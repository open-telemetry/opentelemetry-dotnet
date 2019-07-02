// <copyright file="TracerBaseTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    using System;
    using Moq;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class TracerBaseTest
    {
        private static readonly ITracer noopTracer = TracerBase.NoopTracer;
        private const string SpanName = "MySpanName";
        private readonly TracerBase tracer = Mock.Of<TracerBase>();
        private readonly SpanBuilderBase spanBuilder = new Mock<SpanBuilderBase>(SpanKind.Internal).Object;
        private readonly SpanBase span = Mock.Of<SpanBase>();

        public TracerBaseTest()
        {
        }

        [Fact]
        public void DefaultGetCurrentSpan()
        {
            Assert.Equal(BlankSpan.Instance, noopTracer.CurrentSpan);
        }

        [Fact]
        public void WithSpan_NullSpan()
        {
            Assert.Throws<ArgumentNullException>(() => noopTracer.WithSpan(null));
        }

        [Fact]
        public void GetCurrentSpan_WithSpan()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.CurrentSpan);
            using (noopTracer.WithSpan(span))
            {
                Assert.Same(BlankSpan.Instance, noopTracer.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, noopTracer.CurrentSpan);
        }

        // [Fact]
        // public void wrapRunnable()
        //      {
        //          Runnable runnable;
        //          Assert.Equal(noopTracer.getCurrentSpan()).isSameAs(BlankSpan.Instance);
        //          runnable =
        //              tracer.withSpan(
        //                  span,
        //                  new Runnable() {
        //            @Override
        //                    public void run()
        //          {
        //              Assert.Equal(noopTracer.getCurrentSpan()).isSameAs(span);
        //          }
        //      });
        //  // When we run the runnable we will have the span in the current Context.
        //  runnable.run();
        //  verifyZeroInteractions(span);
        //      Assert.Equal(noopTracer.getCurrentSpan()).isSameAs(BlankSpan.Instance);
        //  }

        // [Fact]
        //  public void wrapCallable() throws Exception
        //    {
        //        readonly Object ret = new Object();
        //    Callable<Object> callable;
        //    Assert.Equal(noopTracer.getCurrentSpan()).isSameAs(BlankSpan.Instance);
        //    callable =
        //        tracer.withSpan(
        //            span,
        //            new Callable<Object>() {
        //              @Override
        //              public Object call() throws Exception
        //    {
        //        Assert.Equal(noopTracer.getCurrentSpan()).isSameAs(span);
        //                return ret;
        //    }
        // });
        //    // When we call the callable we will have the span in the current Context.
        //    Assert.Equal(callable.call()).isEqualTo(ret);
        // verifyZeroInteractions(span);
        // Assert.Equal(noopTracer.getCurrentSpan()).isSameAs(BlankSpan.Instance);
        //  }

        [Fact]
        public void SpanBuilderWithName_NullName()
        {
            Assert.Throws<ArgumentNullException>(() => noopTracer.SpanBuilder(null));
        }

        [Fact]
        public void DefaultSpanBuilderWithName()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilder(SpanName).StartSpan());
        }

        [Fact]
        public void SpanBuilderWithParentAndName_NullName()
        {
            Assert.Throws<ArgumentNullException>(() => noopTracer.SpanBuilderWithParent(name: null, parent: null));
        }

        [Fact]
        public void DefaultSpanBuilderWithParentAndName()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilderWithParent(SpanName, parent: null).StartSpan());
        }

        [Fact]
        public void DefaultSpanBuilderWithParentContext_NullName()
        {
            Assert.Throws<ArgumentNullException>(() => noopTracer.SpanBuilderWithParentContext(null, parentContext: null));
        }

        [Fact]
        public void DefaultSpanBuilderWithParentContext_NullParent()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilderWithParentContext(SpanName, parentContext: null).StartSpan());
        }

        [Fact]
        public void DefaultSpanBuilderWithParentContext()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilderWithParentContext(SpanName, parentContext: SpanContext.Blank).StartSpan());
        }
    }
}
