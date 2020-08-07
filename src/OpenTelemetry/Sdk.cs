// <copyright file="Sdk.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Internal;
using OpenTelemetry.Trace.Samplers;
using static OpenTelemetry.Metrics.MeterProviderSdk;

namespace OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry helper.
    /// </summary>
    public static class Sdk
    {
        public static readonly SuppressInstrumentationScope SuppressInstrumentation = new SuppressInstrumentationScope(false);

        private static readonly TimeSpan DefaultPushInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Creates MeterProvider with the configuration provided.
        /// Configuration involves MetricProcessor, Exporter and push internval.
        /// </summary>
        /// <param name="configure">Action to configure MeterBuilder.</param>
        /// <returns>MeterProvider instance, which must be disposed upon shutdown.</returns>
        public static MeterProvider CreateMeterProvider(Action<MeterBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var meterBuilder = new MeterBuilder();
            configure(meterBuilder);

            var metricProcessor = meterBuilder.MetricProcessor ?? new NoopMetricProcessor();
            var metricExporter = meterBuilder.MetricExporter ?? new NoopMetricExporter();
            var cancellationTokenSource = new CancellationTokenSource();
            var meterRegistry = new Dictionary<MeterRegistryKey, MeterSdk>();

            // We only have PushMetricController now with only configurable thing being the push interval
            var controller = new PushMetricController(
                meterRegistry,
                metricProcessor,
                metricExporter,
                meterBuilder.MetricPushInterval == default ? DefaultPushInterval : meterBuilder.MetricPushInterval,
                cancellationTokenSource);

            var meterProviderSdk = new MeterProviderSdk(metricProcessor, meterRegistry, controller, cancellationTokenSource);

            return meterProviderSdk;
        }

        /// <summary>
        /// Creates TracerProvider with the configuration provided.
        /// This sets up listeners for all configured ActivitySources and
        /// sends activities to the pipeline of Sampler, Processor and Exporter.
        /// </summary>
        /// <param name="configureTracerProviderBuilder">Action to configure TracerProviderBuilder.</param>
        /// <returns>TracerProvider instance, which must be disposed upon shutdown.</returns>
        public static TracerProvider CreateTracerProvider(Action<TracerProviderBuilder> configureTracerProviderBuilder)
        {
            var tracerProviderBuilder = new TracerProviderBuilder();
            configureTracerProviderBuilder?.Invoke(tracerProviderBuilder);

            var tracerProviderSdk = new TracerProviderSdk();
            Sampler sampler = tracerProviderBuilder.Sampler ?? new ParentOrElseSampler(new AlwaysOnSampler());

            ActivityProcessor activityProcessor;
            if (tracerProviderBuilder.ProcessingPipelines == null || !tracerProviderBuilder.ProcessingPipelines.Any())
            {
                // if there are no pipelines are configured, use noop processor
                activityProcessor = new NoopActivityProcessor();
            }
            else if (tracerProviderBuilder.ProcessingPipelines.Count == 1)
            {
                // if there is only one pipeline - use it's outer processor as a
                // single processor on the tracerSdk.
                var processorFactory = tracerProviderBuilder.ProcessingPipelines[0];
                activityProcessor = processorFactory.Build();
            }
            else
            {
                // if there are more pipelines, use processor that will broadcast to all pipelines
                var processors = new ActivityProcessor[tracerProviderBuilder.ProcessingPipelines.Count];

                for (int i = 0; i < tracerProviderBuilder.ProcessingPipelines.Count; i++)
                {
                    processors[i] = tracerProviderBuilder.ProcessingPipelines[i].Build();
                }

                activityProcessor = new FanOutActivityProcessor(processors);
            }

            tracerProviderSdk.Resource = tracerProviderBuilder.Resource;

            var activitySource = new ActivitySourceAdapter(sampler, activityProcessor, tracerProviderSdk.Resource);

            if (tracerProviderBuilder.InstrumentationFactories != null)
            {
                foreach (var instrumentation in tracerProviderBuilder.InstrumentationFactories)
                {
                    tracerProviderSdk.Instrumentations.Add(instrumentation.Factory(activitySource));
                }
            }

            // This is what subscribes to Activities.
            // Think of this as the replacement for DiagnosticListener.AllListeners.Subscribe(onNext => diagnosticListener.Subscribe(..));
            tracerProviderSdk.ActivityListener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = (activity) =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.SetResource(tracerProviderSdk.Resource);
                    }

                    activityProcessor.OnStart(activity);
                },

                // Callback when Activity is stopped.
                ActivityStopped = activityProcessor.OnEnd,

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not.
                ShouldListenTo = (activitySource) => tracerProviderBuilder.ActivitySourceNames?.Contains(activitySource.Name.ToUpperInvariant()) ?? false,

                // Setting this to true means TraceId will be always
                // available in sampling callbacks and will be the actual
                // traceid used, if activity ends up getting created.
                AutoGenerateRootContextTraceId = true,

                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ComputeActivityDataRequest(options, sampler),
            };

            ActivitySource.AddActivityListener(tracerProviderSdk.ActivityListener);
            tracerProviderSdk.ActivityProcessor = activityProcessor;
            return tracerProviderSdk;
        }

        public static TracerProvider CreateTracerProvider(IEnumerable<string> sources, Sampler sampler = null, Resource resource = null)
        {
            var activitySources = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in sources)
            {
                activitySources[name] = true;
            }

            var provider = new TracerProviderSdk
            {
                Resource = resource,
                Sampler = sampler,
            };

            provider.ActivityListener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = (activity) =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.SetResource(resource);
                    }

                    provider.ActivityProcessor?.OnStart(activity);
                },

                // Callback when Activity is stopped.
                ActivityStopped = (activity) =>
                {
                    provider.ActivityProcessor?.OnEnd(activity);
                },

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not.
                ShouldListenTo = (activitySource) => activitySources.ContainsKey(activitySource.Name),

                // Setting this to true means TraceId will be always
                // available in sampling callbacks and will be the actual
                // traceid used, if activity ends up getting created.
                AutoGenerateRootContextTraceId = true,

                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ComputeActivityDataRequest(options, sampler),
            };

            ActivitySource.AddActivityListener(provider.ActivityListener);

            return provider;
        }

        internal static ActivityDataRequest ComputeActivityDataRequest(
            in ActivityCreationOptions<ActivityContext> options,
            Sampler sampler)
        {
            var isRootSpan = /*TODO: Put back once AutoGenerateRootContextTraceId is removed.
                              options.Parent.TraceId == default ||*/ options.Parent.SpanId == default;

            if (sampler != null)
            {
                // As we set ActivityListener.AutoGenerateRootContextTraceId = true,
                // Parent.TraceId will always be the TraceId of the to-be-created Activity,
                // if it get created.
                ActivityTraceId traceId = options.Parent.TraceId;

                var samplingParameters = new SamplingParameters(
                    options.Parent,
                    traceId,
                    options.Name,
                    options.Kind,
                    options.Tags,
                    options.Links);

                var shouldSample = sampler.ShouldSample(samplingParameters);
                if (shouldSample.IsSampled)
                {
                    return ActivityDataRequest.AllDataAndRecorded;
                }
            }

            // If it is the root span select PropagationData so the trace ID is preserved
            // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
            return isRootSpan
                ? ActivityDataRequest.PropagationData
                : ActivityDataRequest.None;
        }
    }
}
