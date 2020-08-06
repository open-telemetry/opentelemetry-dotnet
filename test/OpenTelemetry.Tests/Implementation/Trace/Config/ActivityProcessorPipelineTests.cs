// <copyright file="ActivityProcessorPipelineTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Config
{
    public class ActivityProcessorPipelineTests
    {
        [Fact]
        public void PipelineBuilder_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new ActivityProcessorPipelineBuilder().AddProcessor(null));
            Assert.Throws<ArgumentNullException>(() => new ActivityProcessorPipelineBuilder().SetExporter(null));
            Assert.Throws<ArgumentNullException>(() => new ActivityProcessorPipelineBuilder().SetExportingProcessor(null));
        }

        [Fact]
        public void PipelineBuilder_Defaults()
        {
            var builder = new ActivityProcessorPipelineBuilder();
            Assert.Null(builder.Exporter);
            Assert.Null(builder.Processors);

            var processor = builder.Build();

            Assert.Null(builder.Exporter);
            Assert.Single(builder.Processors);
            Assert.IsType<NoopActivityProcessor>(builder.Processors[0]);
            Assert.Same(processor, builder.Processors[0]);
        }

        [Fact]
        public void PipelineBuilder_AddExporter()
        {
            var builder = new ActivityProcessorPipelineBuilder();

            var exporter = new TestActivityExporter(null);
            builder.SetExporter(exporter);

            Assert.Same(exporter, builder.Exporter);

            var processor = builder.Build();

            Assert.Single(builder.Processors);
            Assert.IsType<BatchingActivityProcessor>(builder.Processors.Single());
            Assert.Same(processor, builder.Processors[0]);
        }

        [Fact]
        public void PipelineBuilder_AddExporterAndExportingProcessor()
        {
            var builder = new ActivityProcessorPipelineBuilder();

            var exporter = new TestActivityExporter(null);
            builder.SetExporter(exporter);

            bool processorFactoryCalled = false;
            builder.SetExportingProcessor(e =>
            {
                processorFactoryCalled = true;
                return new SimpleActivityProcessor(e);
            });

            var processor = builder.Build();

            Assert.Single(builder.Processors);
            Assert.True(processorFactoryCalled);
            Assert.IsType<SimpleActivityProcessor>(builder.Processors.Single());
            Assert.Same(processor, builder.Processors[0]);
        }

        [Fact]
        public void PipelineBuilder_AddExportingProcessor()
        {
            var builder = new ActivityProcessorPipelineBuilder();

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
            var builder = new ActivityProcessorPipelineBuilder();

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
            var builder = new ActivityProcessorPipelineBuilder();

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
            var builder = new ActivityProcessorPipelineBuilder();

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
                    return new SimpleActivityProcessor(exporter);
                })
                .SetExporter(new TestActivityExporter(null));

            var firstProcessor = (TestProcessor)builder.Build();

            Assert.Equal(3, builder.Processors.Count);
            Assert.True(processorFactory1Called);
            Assert.True(processorFactory2Called);
            Assert.True(exportingFactory3Called);

            Assert.Equal("1", firstProcessor.Name);

            var secondProcessor = (TestProcessor)firstProcessor.Next;
            Assert.Equal("2", secondProcessor.Name);
            var thirdProcessor = secondProcessor.Next;
            Assert.IsType<SimpleActivityProcessor>(thirdProcessor);
        }

        private class TestProcessor : ActivityProcessor
        {
            public readonly ActivityProcessor Next;
            public readonly string Name;

            public TestProcessor()
            {
                this.Name = null;
                this.Name = null;
            }

            public TestProcessor(ActivityProcessor next, string name)
            {
                this.Next = next;
                this.Name = name;
            }
        }
    }
}
