﻿// <copyright file="SimpleSpanProcessorTest.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Trace.Export.Test
{
    public class SimpleSpanProcessorTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";

        private TestExporter spanExporter;
        private Tracer tracer;

        public SimpleSpanProcessorTest()
        {
            spanExporter = new TestExporter(null);
            tracer = TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p
                        .SetExporter(spanExporter)
                        .SetExportingProcessor(e => new SimpleSpanProcessor(e)))
                .SetSampler(new AlwaysParentSampler()))
                .GetTracer(null);
        }

        private SpanSdk CreateSampledEndedSpan(string spanName)
        {
            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (SpanSdk)tracer.StartSpan(spanName, context);
            span.End();
            return span;
        }

        private SpanSdk CreateNotSampledEndedSpan(string spanName)
        {
            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            var span = (SpanSdk)tracer.StartSpan(spanName, context);
            span.End();
            return span;
        }


        [Fact]
        public void ThrowsOnNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleSpanProcessor(null));
        }

        [Fact]
        public void ThrowsInExporter()
        {
            spanExporter = new TestExporter(_ => throw new ArgumentException("123"));
            tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p
                        .SetExporter(spanExporter)
                        .SetExportingProcessor(e => new SimpleSpanProcessor(e))))
                .GetTracer(null);

            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (SpanSdk)tracer.StartSpan("foo", context);

            // does not throw
            span.End();
        }

        [Fact]
        public void ProcessorDoesNotBlockOnExporter()
        {
            spanExporter = new TestExporter(async _ => await Task.Delay(500));
            tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p
                        .SetExporter(spanExporter)
                        .SetExportingProcessor(e => new SimpleSpanProcessor(e))))
                .GetTracer(null);

            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (SpanSdk)tracer.StartSpan("foo", context);

            // does not block
            var sw = Stopwatch.StartNew();
            span.End();
            sw.Stop();

            Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            var exported = WaitForSpans(spanExporter, 1, TimeSpan.FromMilliseconds(600));

            Assert.Single(exported);
        }


        [Fact]
        public async Task ShutdownTwice()
        {
            var spanProcessor = new SimpleSpanProcessor(new TestExporter(null));

            await spanProcessor.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);

            // does not throw
            await spanProcessor.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public void ExportDifferentSampledSpans()
        {
            var span1 = CreateSampledEndedSpan(SpanName1);
            var span2 = CreateSampledEndedSpan(SpanName2);

            var exported = WaitForSpans(spanExporter, 2, TimeSpan.FromMilliseconds(100));
            Assert.Equal(2, exported.Length);
            Assert.Contains(new SpanData(span1), exported);
            Assert.Contains(new SpanData(span2), exported);
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

            var exported = WaitForSpans(spanExporter, 1, TimeSpan.FromMilliseconds(100));

            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.Contains(new SpanData(span2), exported);
        }

        public void Dispose()
        {
            spanExporter.ShutdownAsync(CancellationToken.None);
            Activity.Current = null;
        }

        private SpanData[] WaitForSpans(TestExporter exporter, int spanCount, TimeSpan timeout)
        {
            Assert.True(
                SpinWait.SpinUntil(() =>
                    {
                        Thread.Sleep(0);
                        return exporter.ExportedSpans.Length >= spanCount;
                    }, timeout + TimeSpan.FromMilliseconds(20)));

            return exporter.ExportedSpans;
        }
    }
}
