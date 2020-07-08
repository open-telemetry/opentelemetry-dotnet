// <copyright file="SpanProcessorPipelineTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Config
{
    public class SpanProcessorPipelineTests
    {
        [Fact]
        public void PipelineBuilder_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanProcessorPipelineBuilder().AddProcessor(null));
            Assert.Throws<ArgumentNullException>(() => new SpanProcessorPipelineBuilder().SetExporter(null));
            Assert.Throws<ArgumentNullException>(() => new SpanProcessorPipelineBuilder().SetExportingProcessor(null));
        }

        [Fact]
        public void PipelineBuilder_Defaults()
        {
            var builder = new SpanProcessorPipelineBuilder();
            Assert.Null(builder.Exporter);
            Assert.Null(builder.Processors);

            var processor = builder.Build();

            Assert.Null(builder.Exporter);
            Assert.Single(builder.Processors);
            Assert.IsType<NoopSpanProcessor>(builder.Processors[0]);
            Assert.Same(processor, builder.Processors[0]);
        }

        [Fact]
        public void PipelineBuilder_AddExporter()
        {
            var builder = new SpanProcessorPipelineBuilder();

            var exporter = new TestSpanExporter(null);
            builder.SetExporter(exporter);

            Assert.Same(exporter, builder.Exporter);

            var processor = builder.Build();

            Assert.Single(builder.Processors);
            Assert.IsType<BatchingSpanProcessor>(builder.Processors.Single());
            Assert.Same(processor, builder.Processors[0]);
        }

        [Fact]
        public void PipelineBuilder_AddExporterAndExportingProcessor()
        {
            var builder = new SpanProcessorPipelineBuilder();

            var exporter = new TestSpanExporter(null);
            builder.SetExporter(exporter);

            bool processorFactoryCalled = false;
            builder.SetExportingProcessor(e =>
            {
                processorFactoryCalled = true;
                return new SimpleSpanProcessor(e);
            });

            var processor = builder.Build();

            Assert.Single(builder.Processors);
            Assert.True(processorFactoryCalled);
            Assert.IsType<SimpleSpanProcessor>(builder.Processors.Single());
            Assert.Same(processor, builder.Processors[0]);
        }

        [Fact]
        public void PipelineBuilder_AddExportingProcessor()
        {
            var builder = new SpanProcessorPipelineBuilder();

            bool processorFactoryCalled = false;
            var processor = new TestProcessor();
            builder.SetExportingProcessor(e =>
            {
                processorFactoryCalled = true;
                Assert.Null(e);
                return processor;
            });

            Assert.Same(processor, builder.Build());

            Assert.Single(builder.Processors);
            Assert.True(processorFactoryCalled);
            Assert.Same(processor, builder.Processors.Single());
        }

        [Fact]
        public void PipelineBuilder_AddProcessor()
        {
            var builder = new SpanProcessorPipelineBuilder();

            bool processorFactoryCalled = false;
            var processor = new TestProcessor();
            builder.AddProcessor(e =>
            {
                processorFactoryCalled = true;
                return processor;
            });

            Assert.Same(processor, builder.Build());

            Assert.Single(builder.Processors);
            Assert.True(processorFactoryCalled);
            Assert.Same(processor, builder.Processors.Single());
        }

        [Fact]
        public void PipelineBuilder_AddProcessorChain()
        {
            var builder = new SpanProcessorPipelineBuilder();

            bool processorFactory1Called = false;
            bool processorFactory2Called = false;
            bool processorFactory3Called = false;

            builder
                .AddProcessor(next =>
                {
                    processorFactory1Called = true;
                    Assert.NotNull(next);
                    return new TestProcessor(next, "1");
                })
                .AddProcessor(next =>
                {
                    processorFactory2Called = true;
                    Assert.NotNull(next);
                    return new TestProcessor(next, "2");
                })
                .AddProcessor(next =>
                {
                    processorFactory3Called = true;
                    Assert.Null(next);
                    return new TestProcessor(next, "3");
                });

            var firstProcessor = (TestProcessor)builder.Build();

            Assert.Equal(3, builder.Processors.Count);
            Assert.True(processorFactory1Called);
            Assert.True(processorFactory2Called);
            Assert.True(processorFactory3Called);

            Assert.Equal("1", firstProcessor.Name);

            var secondProcessor = (TestProcessor)firstProcessor.Next;
            Assert.Equal("2", secondProcessor.Name);
            var thirdProcessor = (TestProcessor)secondProcessor.Next;
            Assert.Equal("3", thirdProcessor.Name);
        }

        [Fact]
        public void PipelineBuilder_AddProcessorChainWithExporter()
        {
            var builder = new SpanProcessorPipelineBuilder();

            bool processorFactory1Called = false;
            bool processorFactory2Called = false;
            bool exportingFactory3Called = false;

            builder
                .AddProcessor(next =>
                {
                    processorFactory1Called = true;
                    Assert.NotNull(next);
                    return new TestProcessor(next, "1");
                })
                .AddProcessor(next =>
                {
                    processorFactory2Called = true;
                    Assert.NotNull(next);
                    return new TestProcessor(next, "2");
                })
                .SetExportingProcessor(exporter =>
                {
                    exportingFactory3Called = true;
                    Assert.NotNull(exporter);
                    return new SimpleSpanProcessor(exporter);
                })
                .SetExporter(new TestSpanExporter(null));

            var firstProcessor = (TestProcessor)builder.Build();

            Assert.Equal(3, builder.Processors.Count);
            Assert.True(processorFactory1Called);
            Assert.True(processorFactory2Called);
            Assert.True(exportingFactory3Called);

            Assert.Equal("1", firstProcessor.Name);

            var secondProcessor = (TestProcessor)firstProcessor.Next;
            Assert.Equal("2", secondProcessor.Name);
            var thirdProcessor = secondProcessor.Next;
            Assert.IsType<SimpleSpanProcessor>(thirdProcessor);
        }

        private class TestProcessor : SpanProcessor
        {
            public readonly SpanProcessor Next;
            public readonly string Name;

            public TestProcessor()
            {
                this.Name = null;
                this.Name = null;
            }

            public TestProcessor(SpanProcessor next, string name)
            {
                this.Next = next;
                this.Name = name;
            }

            public override void OnStart(SpanData span)
            {
            }

            public override void OnEnd(SpanData span)
            {
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
#if NET452
                return Task.FromResult(0);
#else
                return Task.CompletedTask;
#endif
            }
        }
    }
}
