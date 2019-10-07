// <copyright file="TracerFactory.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace.Configuration
{
    using System;
    using System.Collections.Generic;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;

    public class TracerBuilder : IDisposable
    {
        private TracerConfiguration tracerConfigurationOptions;
        private ISampler sampler = Samplers.AlwaysSample;
        private Func<SpanExporter, SpanProcessor> processorFactory;
        private SpanExporter spanExporter;
        private SpanProcessor spanProcessor;
        private IBinaryFormat binaryFormat = new BinaryFormat();
        private ITextFormat textFormat = new TraceContextFormat();
        private bool isBuilt;

        private List<Func<ITracer, object>> collectorFactories;
        private readonly List<IDisposable> disposables = new List<IDisposable>();

        internal TracerBuilder()
        {
        }

        public TracerBuilder SetSampler(ISampler sampler)
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            this.sampler = sampler;
            return this;
        }

        public TracerBuilder SetExporter(SpanExporter spanExporter)
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            this.spanExporter = spanExporter;
            return this;
        }

        public TracerBuilder SetProcessor(Func<SpanExporter, SpanProcessor> processorFactory)
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            this.processorFactory = processorFactory;
            return this;
        }

        public TracerBuilder AddCollector<TCollector>(
            Func<ITracer, TCollector> collectorFactory)
            where TCollector : class
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            if (this.collectorFactories == null)
            {
                this.collectorFactories = new List<Func<ITracer, object>>();
            }

            this.collectorFactories.Add(collectorFactory);

            return this;
        }

        public TracerBuilder SetTracerOptions(TracerConfiguration options)
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            this.tracerConfigurationOptions = options;
            return this;
        }

        public TracerBuilder SetTextFormat(ITextFormat textFormat)
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            this.textFormat = textFormat;
            return this;
        }

        public TracerBuilder SetBinaryFormat(IBinaryFormat binaryFormat)
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException(nameof(TracerBuilder) + "is already built");
            }

            this.binaryFormat = binaryFormat;
            return this;
        }

        internal ITracer Build(Resource resource)
        {
            ITracer tracer;
            if (!this.isBuilt)
            {
                if (this.tracerConfigurationOptions == null)
                {
                    // TODO separate sampler from options
                    this.tracerConfigurationOptions = new TracerConfiguration(this.sampler);
                }

                if (this.spanExporter == null)
                {
                    // TODO log warning
                    this.spanExporter = new NoopSpanExporter();
                }

                this.spanProcessor = this.processorFactory != null
                    ? this.processorFactory(this.spanExporter)
                    : new BatchingSpanProcessor(this.spanExporter);

                tracer = new Tracer(
                    this.spanProcessor,
                    this.tracerConfigurationOptions,
                    this.binaryFormat,
                    this.textFormat,
                    resource);

                if (this.collectorFactories != null)
                {
                    foreach (var collector in this.collectorFactories)
                    {
                        var collectorInstance = collector.Invoke(tracer);
                        if (collectorInstance is IDisposable disposableCollector)
                        {
                            this.disposables.Add(disposableCollector);
                        }
                    }
                }

                this.isBuilt = true;
            }
            else
            {
                tracer = new Tracer(
                    this.spanProcessor,
                    this.tracerConfigurationOptions,
                    this.binaryFormat,
                    this.textFormat,
                    resource);
            }

            return tracer;
        }

        public void Dispose()
        {
            if (this.spanProcessor is IDisposable disposableProcessor)
            {
                disposableProcessor.Dispose();
            }

            foreach (var disposable in this.disposables)
            {
                disposable.Dispose();
            }
        }
    }

    public class TracerFactory : TracerFactoryBase, IDisposable
    {
        private readonly object lck = new object();
        private readonly Dictionary<TracerRegistryKey, ITracer> tracerRegistry = new Dictionary<TracerRegistryKey, ITracer>();
        private ITracer defaultTracer;
        private readonly TracerBuilder builder;

        private TracerFactory(TracerBuilder builder)
        {
            this.builder = builder;
        }

        public static TracerFactory Create(Action<TracerBuilder> builder)
        {
            var builderInstance = new TracerBuilder();
            builder.Invoke(builderInstance);
            return new TracerFactory(builderInstance);
        }

        public override ITracer GetTracer(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultTracer ?? (this.defaultTracer = this.builder.Build(Resource.Empty));
            }

            lock (this.lck)
            {
                var key = new TracerRegistryKey(name, version);
                if (!this.tracerRegistry.TryGetValue(key, out var tracer))
                {
                    tracer = this.builder.Build(new Resource(CreateLibraryResourceLabels(name, version)));
                    this.tracerRegistry.Add(key, tracer);
                }

                return tracer;
            }
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }

        private static IEnumerable<KeyValuePair<string, string>> CreateLibraryResourceLabels(string name, string version)
        {
            var labels = new Dictionary<string, string> { { "name", name } };
            if (!string.IsNullOrEmpty(version))
            {
                labels.Add("version", version);
            }

            return labels;
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
