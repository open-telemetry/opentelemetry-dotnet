// <copyright file="TracerBuilderTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace
{
    public class TracerBuilderTests
    {
        [Fact]
        public void TracerBuilder_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new TracerBuilder().SetSampler(null));
            Assert.Throws<ArgumentNullException>(() => new TracerBuilder().AddProcessorPipeline(null));
            Assert.Throws<ArgumentNullException>(() => new TracerBuilder().SetTracerOptions(null));
            Assert.Throws<ArgumentNullException>(() => new TracerBuilder().SetBinaryFormat(null));
            Assert.Throws<ArgumentNullException>(() => new TracerBuilder().SetTextFormat(null));
            Assert.Throws<ArgumentNullException>(() => new TracerBuilder().AddCollector<object>(null));
        }

        [Fact]
        public void TracerBuilder_Defaults()
        {
            var builder = new TracerBuilder();
            Assert.Null(builder.Sampler);
            Assert.Null(builder.ProcessingPipelines);
            Assert.Null(builder.BinaryFormat);
            Assert.Null(builder.TextFormat);
            Assert.Null(builder.TracerConfigurationOptions);
            Assert.Null(builder.CollectorFactories);
        }

        [Fact]
        public void TracerBuilder_ValidArgs()
        {
            var builder = new TracerBuilder();

            bool processorFactoryCalled = false;
            bool collectorFactoryCalled = true;

            var sampler = new ProbabilitySampler(0.1);
            var exporter = new TestExporter(_ => { });
            var options = new TracerConfiguration(1, 1, 1);
            var binaryFormat = new BinaryFormat();
            var textFormat = new TraceContextFormat();

            builder
                .SetSampler(sampler)
                .AddProcessorPipeline(p => p
                    .SetExporter(exporter)
                    .SetExportingProcessor(e =>
                    {
                        processorFactoryCalled = true;
                        Assert.Same(e, exporter);
                        return new SimpleSpanProcessor(e);
                    }))
                .SetTracerOptions(options)
                .SetBinaryFormat(binaryFormat)
                .SetTextFormat(textFormat)
                .AddCollector(t =>
                {
                    Assert.NotNull(t);
                    return new TestCollector(t);
                });

            Assert.Same(sampler, builder.Sampler);

            Assert.NotNull(builder.ProcessingPipelines);
            Assert.Single(builder.ProcessingPipelines);
            Assert.Same(exporter, builder.ProcessingPipelines[0].Exporter);

            Assert.NotNull(builder.ProcessingPipelines[0].Build());
            Assert.True(processorFactoryCalled);

            Assert.Same(options, builder.TracerConfigurationOptions);
            Assert.Same(binaryFormat, builder.BinaryFormat);
            Assert.Same(textFormat, builder.TextFormat);
            Assert.Single(builder.CollectorFactories);

            var collectorFactory = builder.CollectorFactories.Single();
            Assert.Equal(nameof(TestCollector), collectorFactory.Name);
            Assert.Equal("semver:" + typeof(TestCollector).Assembly.GetName().Version, collectorFactory.Version);

            Assert.NotNull(collectorFactory.Factory);
            collectorFactory.Factory(new TracerSdk(new SimpleSpanProcessor(exporter), new AlwaysSampleSampler(), options, binaryFormat, textFormat,
                Resource.Empty));

            Assert.True(collectorFactoryCalled);
        }

        private class TestCollector 
        {
            private readonly Tracer tracer;
            public TestCollector(Tracer tracer)
            {
                this.tracer = tracer;
            }
        }
    }
}
