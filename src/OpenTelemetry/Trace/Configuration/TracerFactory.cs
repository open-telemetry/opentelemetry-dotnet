// <copyright file="TracerFactory.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Export.Internal;
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Trace.Configuration
{
    public class TracerFactory : TracerFactoryBase, IDisposable
    {
        private readonly object lck = new object();
        private readonly Dictionary<TracerRegistryKey, Tracer> tracerRegistry = new Dictionary<TracerRegistryKey, Tracer>();
        private readonly List<object> adapters = new List<object>();

        private readonly Sampler sampler;
        private readonly Resource defaultResource;
        private readonly TracerConfiguration configurationOptions;
        private readonly SpanProcessor spanProcessor;

        private Tracer defaultTracer;

        private TracerFactory(TracerBuilder builder)
        {
            this.sampler = builder.Sampler ?? new AlwaysOnSampler();
            this.defaultResource = builder.Resource;

            this.configurationOptions =
                builder.TracerConfigurationOptions ?? new TracerConfiguration();

            if (builder.ProcessingPipelines == null || !builder.ProcessingPipelines.Any())
            {
                // if there are no pipelines are configured, use noop processor
                this.spanProcessor = new NoopSpanProcessor();
            }
            else if (builder.ProcessingPipelines.Count == 1)
            {
                // if there is only one pipeline - use it's outer processor as a
                // single processor on the tracerSdk.
                var processorFactory = builder.ProcessingPipelines[0];
                this.spanProcessor = processorFactory.Build();
            }
            else
            {
                // if there are more pipelines, use processor that will broadcast to all pipelines
                var processors = new SpanProcessor[builder.ProcessingPipelines.Count];

                for (int i = 0; i < builder.ProcessingPipelines.Count; i++)
                {
                    processors[i] = builder.ProcessingPipelines[i].Build();
                }

                this.spanProcessor = new BroadcastProcessor(processors);
            }

            this.defaultTracer = new TracerSdk(
                this.spanProcessor,
                this.sampler,
                this.configurationOptions,
                this.defaultResource);
        }

        /// <summary>
        /// Creates tracerSdk factory.
        /// </summary>
        /// <param name="configure">Function that configures tracerSdk factory.</param>
        /// <returns>Returns new <see cref="TracerFactory"/>.</returns>
        public static TracerFactory Create(Action<TracerBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new TracerBuilder();
            configure(builder);
            var factory = new TracerFactory(builder);

            if (builder.AdapterFactories != null)
            {
                foreach (var adapter in builder.AdapterFactories)
                {
                    var tracer = factory.GetTracer(adapter.Name, adapter.Version);
                    factory.adapters.Add(adapter.Factory(tracer));
                }
            }

            return factory;
        }

        public override Tracer GetTracer(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultTracer;
            }

            lock (this.lck)
            {
                var key = new TracerRegistryKey(name, version);
                if (!this.tracerRegistry.TryGetValue(key, out var tracer))
                {
                    tracer = this.defaultTracer = new TracerSdk(
                        this.spanProcessor,
                        this.sampler,
                        this.configurationOptions,
                        this.defaultResource.Merge(new Resource(CreateLibraryResourceLabels(name, version))));
                    this.tracerRegistry.Add(key, tracer);
                }

                return tracer;
            }
        }

        public void Dispose()
        {
            foreach (var item in this.adapters)
            {
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            this.adapters.Clear();

            if (this.spanProcessor is IDisposable disposableProcessor)
            {
                disposableProcessor.Dispose();
            }
        }

        private static IEnumerable<KeyValuePair<string, object>> CreateLibraryResourceLabels(string name, string version)
        {
            var attributes = new Dictionary<string, object> { [Resource.LibraryNameKey] = name };
            if (!string.IsNullOrEmpty(version))
            {
                attributes.Add(Resource.LibraryVersionKey, version);
            }

            return attributes;
        }

        private readonly struct TracerRegistryKey
        {
            private readonly string name;
            private readonly string version;

            internal TracerRegistryKey(string name, string version)
            {
                this.name = name;
                this.version = version;
            }
        }
    }
}
