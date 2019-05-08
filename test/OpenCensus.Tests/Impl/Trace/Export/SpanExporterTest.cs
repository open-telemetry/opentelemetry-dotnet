// <copyright file="SpanExporterTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Export.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using OpenCensus.Common;
    using OpenCensus.Internal;
    using OpenCensus.Testing.Export;
    using OpenCensus.Trace.Config;
    using OpenCensus.Trace.Internal;
    using Xunit;

    public class SpanExporterTest
    {
        private static readonly String SPAN_NAME_1 = "MySpanName/1";
        private static readonly String SPAN_NAME_2 = "MySpanName/2";
        private readonly RandomGenerator random = new RandomGenerator(1234);
        private readonly ISpanContext sampledSpanContext;
        private readonly ISpanContext notSampledSpanContext;
        private readonly ISpanExporter spanExporter = SpanExporter.Create(4, Duration.Create(1, 0));
        private readonly IRunningSpanStore runningSpanStore = new InProcessRunningSpanStore();
        private readonly IStartEndHandler startEndHandler;
        private SpanOptions recordSpanOptions = SpanOptions.RecordEvents;
        private readonly TestHandler serviceHandler = new TestHandler();
        private IHandler mockServiceHandler = Mock.Of<IHandler>();

        public SpanExporterTest()
        {
            sampledSpanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), TraceOptions.Builder().SetIsSampled(true).Build(), Tracestate.Empty);
            notSampledSpanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), TraceOptions.Default, Tracestate.Empty);
            startEndHandler = new StartEndHandler(spanExporter, runningSpanStore, null, new SimpleEventQueue());

            spanExporter.RegisterHandler("test.service", serviceHandler);
        }

        private Span CreateSampledEndedSpan(string spanName)
        {
            ISpan span =
                Span.StartSpan(
                    sampledSpanContext,
                    recordSpanOptions,
                    spanName,
                    null,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    null);
            span.End();
            return span as Span;
        }

        private Span CreateNotSampledEndedSpan(string spanName)
        {
            ISpan span =
                Span.StartSpan(
                    notSampledSpanContext,
                    recordSpanOptions,
                    spanName,
                    null,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    null);
            span.End();
            return span as Span;
        }

        [Fact]
        public void ExportDifferentSampledSpans()
        {
            Span span1 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span2 = CreateSampledEndedSpan(SPAN_NAME_2);
            var exported = serviceHandler.WaitForExport(2);
            Assert.Equal(2, exported.Count());
            Assert.Contains(span1.ToSpanData(), exported);
            Assert.Contains(span2.ToSpanData(), exported);
        }

        [Fact]
        public void ExportMoreSpansThanTheBufferSize()
        {
            Span span1 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span2 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span3 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span4 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span5 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span6 = CreateSampledEndedSpan(SPAN_NAME_1);
            var exported = serviceHandler.WaitForExport(6);
            Assert.Equal(6, exported.Count());
            Assert.Contains(span1.ToSpanData(), exported);
            Assert.Contains(span2.ToSpanData(), exported);
            Assert.Contains(span3.ToSpanData(), exported);
            Assert.Contains(span4.ToSpanData(), exported);
            Assert.Contains(span5.ToSpanData(), exported);
            Assert.Contains(span6.ToSpanData(), exported);

        }

        [Fact]
        public void InterruptWorkerThreadStops()
        {
  
            Thread serviceExporterThread = ((SpanExporter)spanExporter).ServiceExporterThread;
            spanExporter.Dispose();
            // serviceExporterThread.Interrupt();
            // Test that the worker thread will stop.
            serviceExporterThread.Join();
        }

        [Fact]
        public void ServiceHandlerThrowsException()
        {
            var mockHandler = Mock.Get<IHandler>(mockServiceHandler);
            mockHandler.Setup((h) => h.ExportAsync(It.IsAny<IEnumerable<ISpanData>>())).Throws(new ArgumentException("No export for you."));
            // doThrow(new IllegalArgumentException("No export for you."))
            //    .when(mockServiceHandler)
            //    .export(anyListOf(SpanData));
            spanExporter.RegisterHandler("mock.service", mockServiceHandler);
            Span span1 = CreateSampledEndedSpan(SPAN_NAME_1);
            var exported = serviceHandler.WaitForExport(1);
            Assert.Single(exported);
            Assert.Contains(span1.ToSpanData(), exported);
            // assertThat(exported).containsExactly(span1.toSpanData());
            // Continue to export after the exception was received.
            Span span2 = CreateSampledEndedSpan(SPAN_NAME_1);
            exported = serviceHandler.WaitForExport(1);
            Assert.Single(exported);
            Assert.Contains(span2.ToSpanData(), exported);
            // assertThat(exported).containsExactly(span2.toSpanData());
        }

        [Fact]
        public void ExportSpansToMultipleServices()
        {
            TestHandler serviceHandler2 = new TestHandler();
            spanExporter.RegisterHandler("test.service2", serviceHandler2);
            Span span1 = CreateSampledEndedSpan(SPAN_NAME_1);
            Span span2 = CreateSampledEndedSpan(SPAN_NAME_2);
            var exported1 = serviceHandler.WaitForExport(2);
            var exported2 = serviceHandler2.WaitForExport(2);
            Assert.Equal(2, exported1.Count());
            Assert.Contains(span1.ToSpanData(), exported1);
            Assert.Contains(span2.ToSpanData(), exported1);
            Assert.Equal(2, exported2.Count());
            Assert.Contains(span1.ToSpanData(), exported2);
            Assert.Contains(span2.ToSpanData(), exported2);
        }

        [Fact]
        public void ExportNotSampledSpans()
        {
            Span span1 = CreateNotSampledEndedSpan(SPAN_NAME_1);
            Span span2 = CreateSampledEndedSpan(SPAN_NAME_2);
            // Spans are recorded and exported in the same order as they are ended, we test that a non
            // sampled span is not exported by creating and ending a sampled span after a non sampled span
            // and checking that the first exported span is the sampled span (the non sampled did not get
            // exported).
            var exported = serviceHandler.WaitForExport(1);
            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.DoesNotContain(span1.ToSpanData(), exported);
            Assert.Contains(span2.ToSpanData(), exported);
        }

        [Fact]
        public async Task ExportAsyncCallsAllHandlers()
        {
            var exporter = SpanExporter.Create(4, Duration.Create(1, 0));

            var handler1 = new Mock<IHandler>();
            var handler2 = new Mock<IHandler>();


            exporter.RegisterHandler("first", handler1.Object);
            exporter.RegisterHandler("second", handler2.Object);

            var span1 = new Mock<ISpanData>();
            var span2 = new Mock<ISpanData>();

            await exporter.ExportAsync(new ISpanData[] { span1.Object, span2.Object }, CancellationToken.None);

            Assert.Single(handler1.Invocations);
            var args = (IEnumerable<ISpanData>)handler1.Invocations.First().Arguments.First();


            handler1.Verify(c => c.ExportAsync(It.Is<IEnumerable<ISpanData>>(
                (x) => x.Where((s) => s == span1.Object).Count() > 0 &&
                       x.Where((s) => s == span2.Object).Count() > 0 &&
                       x.Count() == 2)));

            handler2.Verify(c => c.ExportAsync(It.Is<IEnumerable<ISpanData>>(
                (x) => x.Where((s) => s == span1.Object).Count() > 0 &&
                       x.Where((s) => s == span2.Object).Count() > 0 &&
                       x.Count() == 2)));
        }
    }

}
