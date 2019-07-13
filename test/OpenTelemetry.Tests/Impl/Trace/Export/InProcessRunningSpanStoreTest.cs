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

using System;

namespace OpenTelemetry.Trace.Export.Test
{
    using System.Diagnostics;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class InProcessRunningSpanStoreTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";
        private readonly ISpanExporter sampledSpansServiceExporter = SpanExporter.Create(4, Duration.Create(1, 0));
        private readonly InProcessRunningSpanStore activeSpansExporter = new InProcessRunningSpanStore();
        private readonly StartEndHandler startEndHandler;
        private readonly SpanOptions recordSpanOptions = SpanOptions.RecordEvents;

        public InProcessRunningSpanStoreTest()
        {
            startEndHandler = new StartEndHandler(sampledSpansServiceExporter, activeSpansExporter, null, new SimpleEventQueue());
        }

        private ISpan CreateSpan(string spanName)
        {
            var activity = new Activity(spanName).Start();
            return Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordSpanOptions,
                spanName,
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);
        }

        [Fact]
        public void GetSummary_SpansWithDifferentNames()
        {
            var span1 = CreateSpan(SpanName1);
            var span2 = CreateSpan(SpanName2);
            Assert.Equal(2, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SpanName1]
                        .NumRunningSpans);
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SpanName2]
                        .NumRunningSpans);
            span1.End();
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.False(activeSpansExporter.Summary.PerSpanNameSummary.ContainsKey(SpanName1));
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SpanName2]
                        .NumRunningSpans);
            span2.End();
            Assert.Equal(0, activeSpansExporter.Summary.PerSpanNameSummary.Count);
        }

        [Fact]
        public void GetSummary_SpansWithSameName()
        {
            var span1 = CreateSpan(SpanName1);
            var span2 = CreateSpan(SpanName1);
            var span3 = CreateSpan(SpanName1);
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(3,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SpanName1]
                        .NumRunningSpans);
            span1.End();
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(2,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SpanName1]
                        .NumRunningSpans);
            span2.End();
            Assert.Equal(1, activeSpansExporter.Summary.PerSpanNameSummary.Count);
            Assert.Equal(1,
                    activeSpansExporter
                        .Summary
                        .PerSpanNameSummary[SpanName1]
                        .NumRunningSpans);
            span3.End();
            Assert.Equal(0, activeSpansExporter.Summary.PerSpanNameSummary.Count);
        }

        [Fact]
        public void GetActiveSpans_SpansWithDifferentNames()
        {
            var span1 = CreateSpan(SpanName1) as Span;
            var span2 = CreateSpan(SpanName2) as Span;
            Assert.Contains(span1.ToSpanData(), activeSpansExporter.GetRunningSpans(RunningSpanStoreFilter.Create(SpanName1, 0)));
            Assert.Contains(span1.ToSpanData(), activeSpansExporter.GetRunningSpans(RunningSpanStoreFilter.Create(SpanName1, 2)));
            Assert.Contains(span2.ToSpanData(), activeSpansExporter.GetRunningSpans(RunningSpanStoreFilter.Create(SpanName2, 0)));
            span1.End();
            span2.End();
        }

        public void Dispose()
        {
            Activity.Current = null;
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
