// <copyright file="SpanExporterTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Utils;

namespace OpenTelemetry.Trace.Export.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Testing.Export;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class SpanExporterTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";

        private readonly ISpanExporter spanExporter = SpanExporter.Create(4, TimeSpan.FromSeconds(1));
        private readonly IStartEndHandler startEndHandler;
        private readonly TestHandler serviceHandler = new TestHandler();
        private readonly IHandler mockServiceHandler = Mock.Of<IHandler>();

        public SpanExporterTest()
        {
            startEndHandler = new StartEndHandler(spanExporter, new SimpleEventQueue());
            spanExporter.RegisterHandler("test.service", serviceHandler);
        }

        private Span CreateSampledEndedSpan(string spanName)
        {
            var sampledActivity = new Activity(spanName);
            sampledActivity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            sampledActivity.SetIdFormat(ActivityIdFormat.W3C);
            sampledActivity.Start();
            var span =
                Span.StartSpan(
                    sampledActivity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    startEndHandler,
                    PreciseTimestamp.GetUtcNow());
            span.End();
            return span as Span;
        }

        private Span CreateNotSampledEndedSpan(string spanName)
        {
            var notSampledActivity = new Activity(spanName);
            notSampledActivity.SetIdFormat(ActivityIdFormat.W3C);
            notSampledActivity.Start();
            var span =
                Span.StartSpan(
                    notSampledActivity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    startEndHandler,
                    PreciseTimestamp.GetUtcNow());
            span.End();
            return span as Span;
        }

        [Fact]
        public void ExportDifferentSampledSpans()
        {
            var span1 = CreateSampledEndedSpan(SpanName1);
            var span2 = CreateSampledEndedSpan(SpanName2);
            var exported = serviceHandler.WaitForExport(2);
            Assert.Equal(2, exported.Count());
            Assert.Contains(span1.ToSpanData(), exported);
            Assert.Contains(span2.ToSpanData(), exported);
        }

        [Fact]
        public void ExportMoreSpansThanTheBufferSize()
        {
            var span1 = CreateSampledEndedSpan(SpanName1);
            var span2 = CreateSampledEndedSpan(SpanName1);
            var span3 = CreateSampledEndedSpan(SpanName1);
            var span4 = CreateSampledEndedSpan(SpanName1);
            var span5 = CreateSampledEndedSpan(SpanName1);
            var span6 = CreateSampledEndedSpan(SpanName1);
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
  
            var serviceExporterThread = ((SpanExporter)spanExporter).ServiceExporterThread;
            spanExporter.Dispose();
            // serviceExporterThread.Interrupt();
            // Test that the worker thread will stop.
            serviceExporterThread.Join();
        }

        [Fact]
        public void ServiceHandlerThrowsException()
        {
            var mockHandler = Mock.Get<IHandler>(mockServiceHandler);
            mockHandler.Setup((h) => h.ExportAsync(It.IsAny<IEnumerable<SpanData>>())).Throws(new ArgumentException("No export for you."));
            // doThrow(new IllegalArgumentException("No export for you."))
            //    .when(mockServiceHandler)
            //    .export(anyListOf(SpanData));
            spanExporter.RegisterHandler("mock.service", mockServiceHandler);
            var span1 = CreateSampledEndedSpan(SpanName1);
            var exported = serviceHandler.WaitForExport(1);
            Assert.Single(exported);
            Assert.Contains(span1.ToSpanData(), exported);
            // assertThat(exported).containsExactly(span1.toSpanData());
            // Continue to export after the exception was received.
            var span2 = CreateSampledEndedSpan(SpanName1);
            exported = serviceHandler.WaitForExport(1);
            Assert.Single(exported);
            Assert.Contains(span2.ToSpanData(), exported);
            // assertThat(exported).containsExactly(span2.toSpanData());
        }

        [Fact]
        public void ExportSpansToMultipleServices()
        {
            var serviceHandler2 = new TestHandler();
            spanExporter.RegisterHandler("test.service2", serviceHandler2);
            var span1 = CreateSampledEndedSpan(SpanName1);
            var span2 = CreateSampledEndedSpan(SpanName2);
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
            var span1 = CreateNotSampledEndedSpan(SpanName1);
            var span2 = CreateSampledEndedSpan(SpanName2);
            // Spans are recorded and exported in the same order as they are ended, we test that a non
            // sampled span is not exported by creating and ending a sampled span after a non sampled span
            // and checking that the first exported span is the sampled span (the non sampled did not get
            // exported).
            var exported = serviceHandler.WaitForExport(1).ToArray();
            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.Contains(span2.ToSpanData(), exported);
        }

        [Fact]
        public async Task ExportAsyncCallsAllHandlers()
        {
            var exporter = SpanExporter.Create(4, TimeSpan.FromSeconds(1));

            var handler1 = new Mock<IHandler>();

            exporter.RegisterHandler("first", handler1.Object);

            var span1 = CreateSampledEndedSpan(SpanName1).ToSpanData();

            await exporter.ExportAsync(span1, CancellationToken.None);

            Assert.Single(handler1.Invocations);
            var args = (IEnumerable<SpanData>)handler1.Invocations.First().Arguments.First();

            handler1.Verify(c => c.ExportAsync(It.Is<IEnumerable<SpanData>>(
                (x) => x.Any(s => s == span1) && x.Count() == 1)));
        }

        public void Dispose()
        {
            spanExporter?.Dispose();
            Activity.Current = null;
        }
    }

}
