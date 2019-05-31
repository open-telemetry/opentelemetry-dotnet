// <copyright file="InProcessRunningSpanStoreTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export.Test
{
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class InProcessRunningSpanStoreTest
    {

        private static readonly string SPAN_NAME_1 = "MySpanName/1";
        private static readonly string SPAN_NAME_2 = "MySpanName/2";
        private readonly RandomGenerator random = new RandomGenerator(1234);
        private readonly ISpanExporter sampledSpansServiceExporter = SpanExporter.Create(4, Duration.Create(1, 0));
        private readonly InProcessRunningSpanStore activeSpansExporter = new InProcessRunningSpanStore();
        private readonly StartEndHandler startEndHandler;
        private SpanOptions recordSpanOptions = SpanOptions.RecordEvents;

        public InProcessRunningSpanStoreTest()
        {
            startEndHandler = new StartEndHandler(sampledSpansServiceExporter, activeSpansExporter, null, new SimpleEventQueue());
        }

        private ISpan CreateSpan(string spanName)
        {
            ISpanContext spanContext =
                SpanContext.Create(
                    TraceId.GenerateRandomId(random),
                    SpanId.GenerateRandomId(random),
                    TraceOptions.Default, Tracestate.Empty);
            return Span.StartSpan(
                spanContext,
                recordSpanOptions,
                spanName,
                SpanId.GenerateRandomId(random),
                TraceParams.Default,
                startEndHandler,
                null);
        }

        [Fact]
        public void GetSummary_SpansWithDifferentNames()
        {
            ISpan span1 = CreateSpan(SPAN_NAME_1);
            ISpan span2 = CreateSpan(SPAN_NAME_2);
            Assert.Equal(2, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SPAN_NAME_1]
                        .NumRunningSpans);
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SPAN_NAME_2]
                        .NumRunningSpans);
            span1.End();
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.False(activeSpansExporter.Summary.PerSpanNameSummary.ContainsKey(SPAN_NAME_1));
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SPAN_NAME_2]
                        .NumRunningSpans);
            span2.End();
            Assert.Equal(0, activeSpansExporter.Summary.PerSpanNameSummary.Count);
        }

        [Fact]
        public void GetSummary_SpansWithSameName()
        {
            ISpan span1 = CreateSpan(SPAN_NAME_1);
            ISpan span2 = CreateSpan(SPAN_NAME_1);
            ISpan span3 = CreateSpan(SPAN_NAME_1);
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(3,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SPAN_NAME_1]
                        .NumRunningSpans);
            span1.End();
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(2,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SPAN_NAME_1]
                        .NumRunningSpans);
            span2.End();
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SPAN_NAME_1]
                        .NumRunningSpans);
            span3.End();
            Assert.Equal(0, activeSpansExporter.Summary.PerSpanNameSummary.Count);
        }

        [Fact]
        public void GetActiveSpans_SpansWithDifferentNames()
        {
            Span span1 = CreateSpan(SPAN_NAME_1) as Span;
            Span span2 = CreateSpan(SPAN_NAME_2) as Span;
            Assert.Contains(span1.ToSpanData(), activeSpansExporter.GetRunningSpans(RunningSpanStoreFilter.Create(SPAN_NAME_1, 0)));
            Assert.Contains(span1.ToSpanData(), activeSpansExporter.GetRunningSpans(RunningSpanStoreFilter.Create(SPAN_NAME_1, 2)));
            Assert.Contains(span2.ToSpanData(), activeSpansExporter.GetRunningSpans(RunningSpanStoreFilter.Create(SPAN_NAME_2, 0)));
            span1.End();
            span2.End();
        }

        // [Fact]
        // public void getActiveSpans_SpansWithSameName()
        //      {
        //          SpanImpl span1 = createSpan(SPAN_NAME_1);
        //          SpanImpl span2 = createSpan(SPAN_NAME_1);
        //          SpanImpl span3 = createSpan(SPAN_NAME_1);
        //          Assert.Equal(activeSpansExporter.getRunningSpans(Filter.create(SPAN_NAME_1, 0)))
        //              .containsExactly(span1.toSpanData(), span2.toSpanData(), span3.toSpanData());
        //          Assert.Equal(activeSpansExporter.getRunningSpans(Filter.create(SPAN_NAME_1, 2)).size())
        //              .isEqualTo(2);
        //          Assert.Equal(activeSpansExporter.getRunningSpans(Filter.create(SPAN_NAME_1, 2)))
        //              .containsAnyOf(span1.toSpanData(), span2.toSpanData(), span3.toSpanData());
        //          span1.end();
        //          span2.end();
        //          span3.end();
        //      }
    }
}
