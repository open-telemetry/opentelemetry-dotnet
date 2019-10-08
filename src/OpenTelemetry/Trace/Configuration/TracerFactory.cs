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

    public class TracerBuilder
    {
        internal TracerConfigurationOptions TracerConfigurationOptions { get; private set; }
        internal ISampler Sampler { get; private set; }
        internal Func<SpanExporter, SpanProcessor> ProcessorFactory { get; private set; }
        internal SpanExporter SpanExporter;
        internal IBinaryFormat BinaryFormat { get; private set; }
        internal ITextFormat TextFormat { get; private set; }

        internal List<CollectorFactory> CollectorFactories { get; private set; }

        internal TracerBuilder()
        {
        }

        public TracerBuilder SetSampler(ISampler sampler)
        {
            this.Sampler = sampler;
            return this;
        }

        public TracerBuilder SetExporter(SpanExporter spanExporter)
        {
            this.SpanExporter = spanExporter;
            return this;
        }

        public TracerBuilder SetProcessor(Func<SpanExporter, SpanProcessor> processorFactory)
        {
            this.ProcessorFactory = processorFactory;
            return this;
        }

        public TracerBuilder AddCollector<TCollector>(
            Func<ITracer, TCollector> collectorFactory)
            where TCollector : class
        {
            if (this.CollectorFactories == null)
            {
                this.CollectorFactories = new List<CollectorFactory>();
            }

            this.CollectorFactories.Add(new CollectorFactory(typeof(TCollector).Name, null /*TODO*/, collectorFactory));

            return this;
        }

        public TracerBuilder SetTracerOptions(TracerConfigurationOptions options)
        {
            this.TracerConfigurationOptions = options;
            return this;
        }

        public TracerBuilder SetTextFormat(ITextFormat textFormat)
        {
            this.TextFormat = textFormat;
            return this;
        }

        public TracerBuilder SetBinaryFormat(IBinaryFormat binaryFormat)
        {
            this.BinaryFormat = binaryFormat;
            return this;
        }

        internal readonly struct CollectorFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<ITracer, object> Factory;

            internal CollectorFactory(string name, string version, Func<ITracer, object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }
    }

    public class TracerFactory : TracerFactoryBase, IDisposable
    {
        private readonly object lck = new object();
        private readonly Dictionary<TracerRegistryKey, ITracer> tracerRegistry = new Dictionary<TracerRegistryKey, ITracer>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();

        private ITracer defaultTracer;
        private readonly ISampler sampler;
        private readonly TracerConfigurationOptions configurationOptions;
        private readonly SpanExporter exporter;
        private readonly SpanProcessor spanProcessor;
        private readonly IBinaryFormat binaryFormat;
        private readonly ITextFormat textFormat;

        private TracerFactory(TracerBuilder builder)
        {
            this.sampler = builder.Sampler ?? Samplers.AlwaysSample;

            // TODO separate sampler from options
            this.configurationOptions =
                builder.TracerConfigurationOptions ?? new TracerConfigurationOptions(this.sampler);

            // TODO log warning (or throw?) if there is no exporter
            this.exporter = builder.SpanExporter ?? new NoopSpanExporter();

            this.spanProcessor = builder.ProcessorFactory != null ? 
                builder.ProcessorFactory(this.exporter) :
                new BatchingSpanProcessor(this.exporter);

            this.binaryFormat = builder.BinaryFormat ?? new BinaryFormat();
            this.textFormat = builder.TextFormat ?? new TraceContextFormat();

            this.defaultTracer = new Tracer(
                this.spanProcessor,
                this.configurationOptions,
                this.binaryFormat,
                this.textFormat,
                Resource.Empty);
        }

        public static TracerFactory Create(Action<TracerBuilder> configure)
        {
            var builder = new TracerBuilder();
            configure(builder);
            var factory = new TracerFactory(builder);

            if (builder.CollectorFactories != null)
            {
                foreach (var collector in builder.CollectorFactories)
                {
                    var tracer = factory.GetTracer(collector.Name, collector.Version);

                    var collectorInstance = collector.Factory(tracer);
                    if (collectorInstance is IDisposable disposableCollector)
                    {
                        factory.disposables.Add(disposableCollector);
                    }
                }
            }

            return factory;
        }

        public override ITracer GetTracer(string name, string version = null)
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
                    tracer = this.defaultTracer = new Tracer(
                        this.spanProcessor,
                        this.configurationOptions,
                        this.binaryFormat,
                        this.textFormat,
                        new Resource(CreateLibraryResourceLabels(name, version)));
                    this.tracerRegistry.Add(key, tracer);
                }

                return tracer;
            }
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
