// <copyright file="TracerProviderSdk.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    internal sealed class TracerProviderSdk : TracerProvider
    {
        internal int ShutdownCount;

        private readonly List<object> instrumentations = new List<object>();
        private readonly ActivityListener listener;
        private readonly Sampler sampler;
        private readonly Action<Activity> getRequestedDataAction;
        private readonly bool supportLegacyActivity;
        private BaseProcessor<Activity> processor;

        internal TracerProviderSdk(
            Resource resource,
            IEnumerable<string> sources,
            IEnumerable<TracerProviderBuilderBase.InstrumentationFactory> instrumentationFactories,
            Sampler sampler,
            List<BaseProcessor<Activity>> processors,
            Dictionary<string, bool> legacyActivityOperationNames)
        {
            this.Resource = resource;
            this.sampler = sampler;
            this.supportLegacyActivity = legacyActivityOperationNames.Count > 0;

            bool legacyActivityWildcardMode = false;
            Regex legacyActivityWildcardModeRegex = null;
            foreach (var legacyName in legacyActivityOperationNames)
            {
                if (legacyName.Key.Contains('*'))
                {
                    legacyActivityWildcardMode = true;
                    legacyActivityWildcardModeRegex = GetWildcardRegex(legacyActivityOperationNames.Keys);
                    break;
                }
            }

            foreach (var processor in processors)
            {
                this.AddProcessor(processor);
            }

            if (instrumentationFactories.Any())
            {
                foreach (var instrumentationFactory in instrumentationFactories)
                {
                    this.instrumentations.Add(instrumentationFactory.Factory());
                }
            }

            var listener = new ActivityListener();

            if (this.supportLegacyActivity)
            {
                Func<Activity, bool> legacyActivityPredicate = null;
                if (legacyActivityWildcardMode)
                {
                    legacyActivityPredicate = activity => legacyActivityWildcardModeRegex.IsMatch(activity.OperationName);
                }
                else
                {
                    legacyActivityPredicate = activity => legacyActivityOperationNames.ContainsKey(activity.OperationName);
                }

                listener.ActivityStarted = activity =>
                {
                    OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);

                    if (string.IsNullOrEmpty(activity.Source.Name))
                    {
                        if (legacyActivityPredicate(activity))
                        {
                            // Legacy activity matches the user configured list.
                            // Call sampler for the legacy activity
                            // unless suppressed.
                            if (!Sdk.SuppressInstrumentation)
                            {
                                this.getRequestedDataAction(activity);
                            }
                            else
                            {
                                activity.IsAllDataRequested = false;
                            }
                        }
                        else
                        {
                            // Legacy activity doesn't match the user configured list. No need to proceed further.
                            return;
                        }
                    }

                    if (!activity.IsAllDataRequested)
                    {
                        return;
                    }

                    if (SuppressInstrumentationScope.IncrementIfTriggered() == 0)
                    {
                        this.processor?.OnStart(activity);
                    }
                };

                listener.ActivityStopped = activity =>
                {
                    OpenTelemetrySdkEventSource.Log.ActivityStopped(activity);

                    if (string.IsNullOrEmpty(activity.Source.Name) && !legacyActivityPredicate(activity))
                    {
                        // Legacy activity doesn't match the user configured list. No need to proceed further.
                        return;
                    }

                    if (!activity.IsAllDataRequested)
                    {
                        return;
                    }

                    // Spec says IsRecording must be false once span ends.
                    // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#isrecording
                    // However, Activity has slightly different semantic
                    // than Span and we don't have strong reason to do this
                    // now, as Activity anyway allows read/write always.
                    // Intentionally commenting the following line.
                    // activity.IsAllDataRequested = false;

                    if (SuppressInstrumentationScope.DecrementIfTriggered() == 0)
                    {
                        this.processor?.OnEnd(activity);
                    }
                };
            }
            else
            {
                listener.ActivityStarted = activity =>
                {
                    OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);

                    if (activity.IsAllDataRequested && SuppressInstrumentationScope.IncrementIfTriggered() == 0)
                    {
                        this.processor?.OnStart(activity);
                    }
                };

                listener.ActivityStopped = activity =>
                {
                    OpenTelemetrySdkEventSource.Log.ActivityStopped(activity);

                    if (!activity.IsAllDataRequested)
                    {
                        return;
                    }

                    // Spec says IsRecording must be false once span ends.
                    // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#isrecording
                    // However, Activity has slightly different semantic
                    // than Span and we don't have strong reason to do this
                    // now, as Activity anyway allows read/write always.
                    // Intentionally commenting the following line.
                    // activity.IsAllDataRequested = false;

                    if (SuppressInstrumentationScope.DecrementIfTriggered() == 0)
                    {
                        this.processor?.OnEnd(activity);
                    }
                };
            }

            if (sampler is AlwaysOnSampler)
            {
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
                this.getRequestedDataAction = this.RunGetRequestedDataAlwaysOnSampler;
            }
            else if (sampler is AlwaysOffSampler)
            {
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? PropagateOrIgnoreData(options.Parent.TraceId) : ActivitySamplingResult.None;
                this.getRequestedDataAction = this.RunGetRequestedDataAlwaysOffSampler;
            }
            else
            {
                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? ComputeActivitySamplingResult(options, sampler) : ActivitySamplingResult.None;
                this.getRequestedDataAction = this.RunGetRequestedDataOtherSampler;
            }

            if (sources.Any())
            {
                // Sources can be null. This happens when user
                // is only interested in InstrumentationLibraries
                // which do not depend on ActivitySources.

                var wildcardMode = false;

                // Validation of source name is already done in builder.
                foreach (var name in sources)
                {
                    if (name.Contains('*'))
                    {
                        wildcardMode = true;
                        break;
                    }
                }

                if (wildcardMode)
                {
                    var regex = GetWildcardRegex(sources);

                    // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                    // or not.
                    listener.ShouldListenTo = (activitySource) =>
                        this.supportLegacyActivity ?
                        string.IsNullOrEmpty(activitySource.Name) || regex.IsMatch(activitySource.Name) :
                        regex.IsMatch(activitySource.Name);
                }
                else
                {
                    var activitySources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var name in sources)
                    {
                        activitySources.Add(name);
                    }

                    if (this.supportLegacyActivity)
                    {
                        activitySources.Add(string.Empty);
                    }

                    // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                    // or not.
                    listener.ShouldListenTo = (activitySource) => activitySources.Contains(activitySource.Name);
                }
            }
            else
            {
                if (this.supportLegacyActivity)
                {
                    listener.ShouldListenTo = (activitySource) => string.IsNullOrEmpty(activitySource.Name);
                }
            }

            ActivitySource.AddActivityListener(listener);
            this.listener = listener;

            Regex GetWildcardRegex(IEnumerable<string> collection)
            {
                var pattern = '^' + string.Join("|", from name in collection select "(?:" + Regex.Escape(name).Replace("\\*", ".*") + ')') + '$';
                return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }

        internal Resource Resource { get; }

        internal List<object> Instrumentations => this.instrumentations;

        internal BaseProcessor<Activity> Processor => this.processor;

        internal Sampler Sampler => this.sampler;

        internal TracerProviderSdk AddProcessor(BaseProcessor<Activity> processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            processor.SetParentProvider(this);

            if (this.processor == null)
            {
                this.processor = processor;
            }
            else if (this.processor is CompositeProcessor<Activity> compositeProcessor)
            {
                compositeProcessor.AddProcessor(processor);
            }
            else
            {
                this.processor = new CompositeProcessor<Activity>(new[]
                {
                    this.processor,
                    processor,
                });
            }

            return this;
        }

        internal bool OnForceFlush(int timeoutMilliseconds)
        {
            return this.processor?.ForceFlush(timeoutMilliseconds) ?? true;
        }

        /// <summary>
        /// Called by <c>Shutdown</c>. This function should block the current
        /// thread until shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which made the
        /// first call to <c>Shutdown</c>. This function should not throw
        /// exceptions.
        /// </remarks>
        internal bool OnShutdown(int timeoutMilliseconds)
        {
            // TO DO Put OnShutdown logic in a task to run within the user provider timeOutMilliseconds
            bool? result;
            if (this.instrumentations != null)
            {
                foreach (var item in this.instrumentations)
                {
                    (item as IDisposable)?.Dispose();
                }

                this.instrumentations.Clear();
            }

            result = this.processor?.Shutdown(timeoutMilliseconds);
            this.listener?.Dispose();
            return result ?? true;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.instrumentations != null)
            {
                foreach (var item in this.instrumentations)
                {
                    (item as IDisposable)?.Dispose();
                }

                this.instrumentations.Clear();
            }

            (this.sampler as IDisposable)?.Dispose();

            // Wait for up to 5 seconds grace period
            this.processor?.Shutdown(5000);
            this.processor?.Dispose();

            // Shutdown the listener last so that anything created while instrumentation cleans up will still be processed.
            // Redis instrumentation, for example, flushes during dispose which creates Activity objects for any profiling
            // sessions that were open.
            this.listener?.Dispose();

            base.Dispose(disposing);
        }

        private static ActivitySamplingResult ComputeActivitySamplingResult(
            in ActivityCreationOptions<ActivityContext> options,
            Sampler sampler)
        {
            var samplingParameters = new SamplingParameters(
                options.Parent,
                options.TraceId,
                options.Name,
                options.Kind,
                options.Tags,
                options.Links);

            var shouldSample = sampler.ShouldSample(samplingParameters);

            var activitySamplingResult = shouldSample.Decision switch
            {
                SamplingDecision.RecordAndSample => ActivitySamplingResult.AllDataAndRecorded,
                SamplingDecision.RecordOnly => ActivitySamplingResult.AllData,
                _ => ActivitySamplingResult.PropagationData
            };

            if (activitySamplingResult != ActivitySamplingResult.PropagationData)
            {
                foreach (var att in shouldSample.Attributes)
                {
                    options.SamplingTags.Add(att.Key, att.Value);
                }

                return activitySamplingResult;
            }

            return PropagateOrIgnoreData(options.Parent.TraceId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActivitySamplingResult PropagateOrIgnoreData(ActivityTraceId traceId)
        {
            var isRootSpan = traceId == default;

            // If it is the root span select PropagationData so the trace ID is preserved
            // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
            return isRootSpan
                ? ActivitySamplingResult.PropagationData
                : ActivitySamplingResult.None;
        }

        private void RunGetRequestedDataAlwaysOnSampler(Activity activity)
        {
            activity.IsAllDataRequested = true;
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
        }

        private void RunGetRequestedDataAlwaysOffSampler(Activity activity)
        {
            activity.IsAllDataRequested = false;
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }

        private void RunGetRequestedDataOtherSampler(Activity activity)
        {
            ActivityContext parentContext;

            // Check activity.ParentId alone is sufficient to normally determine if a activity is root or not. But if one uses activity.SetParentId to override the TraceId (without intending to set an actual parent), then additional check of parentspanid being empty is required to confirm if an activity is root or not.
            // This checker can be removed, once Activity exposes an API to customize ID Generation (https://github.com/dotnet/runtime/issues/46704) or issue https://github.com/dotnet/runtime/issues/46706 is addressed.
            if (string.IsNullOrEmpty(activity.ParentId) || activity.ParentSpanId.ToHexString() == "0000000000000000")
            {
                parentContext = default;
            }
            else if (activity.Parent != null)
            {
                parentContext = activity.Parent.Context;
            }
            else
            {
                parentContext = new ActivityContext(
                    activity.TraceId,
                    activity.ParentSpanId,
                    activity.ActivityTraceFlags,
                    activity.TraceStateString,
                    isRemote: true);
            }

            var samplingParameters = new SamplingParameters(
                parentContext,
                activity.TraceId,
                activity.DisplayName,
                activity.Kind,
                activity.TagObjects,
                activity.Links);

            var samplingResult = this.sampler.ShouldSample(samplingParameters);

            switch (samplingResult.Decision)
            {
                case SamplingDecision.Drop:
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    break;
                case SamplingDecision.RecordOnly:
                    activity.IsAllDataRequested = true;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    break;
                case SamplingDecision.RecordAndSample:
                    activity.IsAllDataRequested = true;
                    activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                    break;
            }

            if (samplingResult.Decision != SamplingDecision.Drop)
            {
                foreach (var att in samplingResult.Attributes)
                {
                    activity.SetTag(att.Key, att.Value);
                }
            }
        }
    }
}
