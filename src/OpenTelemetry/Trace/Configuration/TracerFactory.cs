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

    public class TracerFactory : TracerFactoryBase, IDisposable
    {
        private readonly object lck = new object();
        private readonly Dictionary<TracerRegistryKey, ITracer> tracerRegistry = new Dictionary<TracerRegistryKey, ITracer>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();

        private TracerConfiguration tracerConfigurationOptions;
        private ISampler sampler;
        private Func<SpanExporter, SpanProcessor> processorFactory;
        private SpanExporter spanExporter;
        private SpanProcessor spanProcessor;
        private IBinaryFormat binaryFormat = new BinaryFormat();
        private ITextFormat textFormat = new TraceContextFormat();

        private List<Collector> collectorFactories;
        private ITracer defaultTracer;
        private bool isBuilt;
        
        public TracerFactory SetSampler(ISampler sampler)
        {
            this.sampler = sampler;
            return this;
        }

        public TracerFactory SetExporter(SpanExporter spanExporter)
        {
            this.spanExporter = spanExporter;
            return this;
        }

        public TracerFactory SetProcessor(Func<SpanExporter, SpanProcessor> processorFactory)
        {
            this.processorFactory = processorFactory;
            return this;
        }

        public TracerFactory AddCollector<TCollector>(
            Func<ITracer, TCollector> collectorFactory)
            where TCollector : class
        {
            if (this.collectorFactories == null)
            {
                this.collectorFactories = new List<Collector>();
            }

            this.collectorFactories.Add(new Collector(typeof(TCollector).Name, null, collectorFactory));

            return this;
        }

        public TracerFactory SetTracerOptions(TracerConfiguration options)
        {
            this.tracerConfigurationOptions = options;
            return this;
        }

        public TracerFactory SetTextFormat(ITextFormat textFormat)
        {
            this.textFormat = textFormat;
            return this;
        }

        public TracerFactory SetBinaryFormat(IBinaryFormat binaryFormat)
        {
            this.binaryFormat = binaryFormat;
            return this;
        }

        public override ITracer GetTracer(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultTracer ?? (this.defaultTracer = this.Build(Resource.Empty));
            }

            lock (this.lck)
            {
                var key = new TracerRegistryKey(name, version);
                if (!this.tracerRegistry.TryGetValue(key, out var tracer))
                {
                    tracer = this.Build(new Resource(CreateLibraryResourceLabels(name, version)));
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

        internal ITracer Build(Resource resource)
        {
            ITracer tracer;
            if (!this.isBuilt)
            {
                if (this.sampler == null)
                {
                    // TODO separate sampler from options
                    this.sampler = Samplers.AlwaysSample;
                }

                if (this.tracerConfigurationOptions == null)
                {
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
                        var collectorInstance = collector.Factory.Invoke(tracer);
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

        internal void InitDefaultFactory()
        {
            this.Init(this);
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

        private struct Collector
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<ITracer, object> Factory;

            internal Collector(string name, string version, Func<ITracer, object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }

        private struct TracerRegistryKey
        {
            private string name;
            private string version;

            internal TracerRegistryKey(string name, string version)
            {
                this.name = name;
                this.version = version;
            }
        }
    }
}
