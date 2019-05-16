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
    using Internal;
    using Moq;
    using OpenTelemetry.Common;
    using Xunit;

    public class TracerBaseTest
    {
        private static readonly ITracer noopTracer = TracerBase.NoopTracer;
        private static readonly string SPAN_NAME = "MySpanName";
        private TracerBase tracer = Mock.Of<TracerBase>();
        private SpanBuilderBase spanBuilder = new Mock<SpanBuilderBase>(SpanKind.Internal).Object;
        private SpanBase span = Mock.Of<SpanBase>();

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
            IScope ws = noopTracer.WithSpan(span);
            try
            {
                Assert.Same(span, noopTracer.CurrentSpan);
            }
            finally
            {
                ws.Dispose();
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
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilder(SPAN_NAME).StartSpan());
        }

        [Fact]
        public void SpanBuilderWithParentAndName_NullName()
        {
            Assert.Throws<ArgumentNullException>(() => noopTracer.SpanBuilderWithExplicitParent(spanName: null, parent: null));
        }

        [Fact]
        public void DefaultSpanBuilderWithParentAndName()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilderWithExplicitParent(SPAN_NAME, parent: null).StartSpan());
        }

        [Fact]
        public void spanBuilderWithRemoteParent_NullName()
        {
            Assert.Throws<ArgumentNullException>(() => noopTracer.SpanBuilderWithRemoteParent(null, remoteParentSpanContext: null));
        }

        [Fact]
        public void DefaultSpanBuilderWithRemoteParent_NullParent()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilderWithRemoteParent(SPAN_NAME, remoteParentSpanContext: null).StartSpan());
        }

        [Fact]
        public void DefaultSpanBuilderWithRemoteParent()
        {
            Assert.Same(BlankSpan.Instance, noopTracer.SpanBuilderWithRemoteParent(SPAN_NAME, remoteParentSpanContext: SpanContext.Invalid).StartSpan());
        }

        [Fact]
        public void StartSpanWithParentFromContext()
        {
            IScope ws = tracer.WithSpan(span);
            try
            {
                Assert.Same(span, tracer.CurrentSpan);
                Mock.Get(tracer).Setup((tracer) => tracer.SpanBuilderWithExplicitParent(SPAN_NAME, SpanKind.Internal, span)).Returns(spanBuilder);
                Assert.Same(spanBuilder, tracer.SpanBuilder(SPAN_NAME));
            }
            finally
            {
                ws.Dispose();
            }
        }

        [Fact]
        public void StartSpanWithInvalidParentFromContext()
        {
            IScope ws = tracer.WithSpan(BlankSpan.Instance);
            try
            {
                Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
                Mock.Get(tracer).Setup((t) => t.SpanBuilderWithExplicitParent(SPAN_NAME, SpanKind.Internal, BlankSpan.Instance)).Returns(spanBuilder);
                Assert.Same(spanBuilder, tracer.SpanBuilder(SPAN_NAME));
            }
            finally
            {
                ws.Dispose();
            }
        }
    }
}
