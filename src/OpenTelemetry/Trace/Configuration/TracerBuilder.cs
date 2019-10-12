// <copyright file="TracerBuilder.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace.Configuration
{
    public class TracerBuilder
    {
        internal TracerBuilder()
        {
        }

        internal TracerConfiguration TracerConfigurationOptions { get; private set; }

        internal ISampler Sampler { get; private set; }

        internal Func<SpanExporter, SpanProcessor> ProcessorFactory { get; private set; }

        internal SpanExporter SpanExporter { get; private set; }

        internal IBinaryFormat BinaryFormat { get; private set; }

        internal ITextFormat TextFormat { get; private set; }

        internal List<CollectorFactory> CollectorFactories { get; private set; }

        public TracerBuilder SetSampler(ISampler sampler)
        {
            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        public TracerBuilder SetExporter(SpanExporter spanExporter)
        {
            this.SpanExporter = spanExporter ?? throw new ArgumentNullException(nameof(spanExporter));
            return this;
        }

        public TracerBuilder SetProcessor(Func<SpanExporter, SpanProcessor> processorFactory)
        {
            this.ProcessorFactory = processorFactory ?? throw new ArgumentNullException(nameof(processorFactory));
            return this;
        }

        public TracerBuilder AddCollector<TCollector>(
            Func<ITracer, TCollector> collectorFactory)
            where TCollector : class
        {
            if (collectorFactory == null)
            {
                throw new ArgumentNullException(nameof(collectorFactory));
            }

            if (this.CollectorFactories == null)
            {
                this.CollectorFactories = new List<CollectorFactory>();
            }

            this.CollectorFactories.Add(new CollectorFactory(typeof(TCollector).Name, "semver:" + typeof(TCollector).Assembly.GetName().Version, collectorFactory));

            return this;
        }

        public TracerBuilder SetTracerOptions(TracerConfiguration options)
        {
            this.TracerConfigurationOptions = options ?? throw new ArgumentNullException(nameof(options));
            return this;
        }

        public TracerBuilder SetTextFormat(ITextFormat textFormat)
        {
            this.TextFormat = textFormat ?? throw new ArgumentNullException(nameof(textFormat));
            return this;
        }

        public TracerBuilder SetBinaryFormat(IBinaryFormat binaryFormat)
        {
            this.BinaryFormat = binaryFormat ?? throw new ArgumentNullException(nameof(binaryFormat));
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
}
