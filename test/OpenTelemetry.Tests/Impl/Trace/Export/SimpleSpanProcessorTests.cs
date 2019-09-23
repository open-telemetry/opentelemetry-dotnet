// <copyright file="SimpleSpanProcessorTest.cs" company="OpenTelemetry Authors">
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
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using OpenTelemetry.Testing.Export;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Utils;
    using Xunit;

    // TODO: review tests
    // failure tests
    // batch tests
    // multi processors tests
    public class SimpleSpanProcessorTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";

        private TestExporter spanExporter = new TestExporter(false);
        private SpanProcessor spanProcessor;

        public SimpleSpanProcessorTest()
        {
            spanProcessor = new SimpleSpanProcessor(spanExporter);
        }

        private Span CreateSampledEndedSpan(string spanName)
        {
            var sampledActivity = new Activity(spanName);
            sampledActivity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            sampledActivity.SetIdFormat(ActivityIdFormat.W3C);
            sampledActivity.Start();
            var span =
                new Span(
                    sampledActivity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    default);
            span.End();
            return span;
        }

        private Span CreateNotSampledEndedSpan(string spanName)
        {
            var notSampledActivity = new Activity(spanName);
            notSampledActivity.SetIdFormat(ActivityIdFormat.W3C);
            notSampledActivity.Start();
            var span =
                new Span(
                    notSampledActivity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);
            span.End();
            return span;
        }

        [Fact]
        public void ThrowsOnNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleSpanProcessor(null));
        }

        [Fact]
        public void ExportDifferentSampledSpans()
        {
            var span1 = CreateSampledEndedSpan(SpanName1);
            var span2 = CreateSampledEndedSpan(SpanName2);
            var exported = spanExporter.WaitForExport(2);
            Assert.Equal(2, exported.Count());
            Assert.Contains(span1, exported);
            Assert.Contains(span2, exported);
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
            var exported = spanExporter.WaitForExport(6);
            Assert.Equal(6, exported.Count());
            Assert.Contains(span1, exported);
            Assert.Contains(span2, exported);
            Assert.Contains(span3, exported);
            Assert.Contains(span4, exported);
            Assert.Contains(span5, exported);
            Assert.Contains(span6, exported);
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
            var exported = spanExporter.WaitForExport(1).ToArray();
            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.Contains(span2, exported);
        }

        public void Dispose()
        {
            spanExporter.ShutdownAsync(CancellationToken.None);
            Activity.Current = null;
        }
    }
}
