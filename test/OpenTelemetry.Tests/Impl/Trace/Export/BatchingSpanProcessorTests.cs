// <copyright file="BatchingSpanProcessorTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Sampler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Trace.Export.Test
{
    public class BatchingSpanProcessorTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";

        private static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        private Span CreateSampledEndedSpan(string spanName, SpanProcessor spanProcessor)
        {
            var tracer = TracerFactory.Create(b => b
                .SetSampler(Samplers.AlwaysSample)
                .AddProcessorPipeline(p => p.AddProcessor(e => spanProcessor))
                .SetTracerOptions(new TracerConfiguration())).GetTracer(null);
            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (Span)tracer.StartSpan(spanName, context);
            span.End();
            return span;
        }

        private Span CreateNotSampledEndedSpan(string spanName, SpanProcessor spanProcessor)
        {
            var tracer = TracerFactory.Create(b => b
                .SetSampler(Samplers.NeverSample)
                .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor))
                .SetTracerOptions(new TracerConfiguration())).GetTracer(null);
            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            var span = (Span)tracer.StartSpan(spanName, context);
            span.End();
            return span;
        }

        [Fact]
        public void ThrowsOnInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchingSpanProcessor(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingSpanProcessor(new TestExporter(null), 0, TimeSpan.FromSeconds(5), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingSpanProcessor(new TestExporter(null), 2048, TimeSpan.FromSeconds(5), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingSpanProcessor(new TestExporter(null), 512, TimeSpan.FromSeconds(5), 513));
        }

        [Fact]
        public async Task ShutdownTwice()
        {
            using (var spanProcessor = new BatchingSpanProcessor(new TestExporter(null)))
            {

                await spanProcessor.ShutdownAsync(CancellationToken.None);

                // does not throw
                await spanProcessor.ShutdownAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task ShutdownWithHugeScheduleDelay()
        {
            using (var spanProcessor =
                new BatchingSpanProcessor(new TestExporter(null), 128, TimeSpan.FromMinutes(1), 32))
            {

                var sw = Stopwatch.StartNew();
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
                {
                    cts.Token.ThrowIfCancellationRequested();
                    await spanProcessor.ShutdownAsync(cts.Token).ConfigureAwait(false);
                }

                sw.Stop();
                Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
            }
        }

        [Fact]
        public void ExportDifferentSampledSpans()
        {
            var spanExporter = new TestExporter(null);
            using (var spanProcessor = new BatchingSpanProcessor(spanExporter, 128, DefaultDelay, 128))
            {
                var span1 = CreateSampledEndedSpan(SpanName1, spanProcessor);
                var span2 = CreateSampledEndedSpan(SpanName2, spanProcessor);

                var exported = WaitForSpans(spanExporter, 2, DefaultTimeout);

                Assert.Equal(2, exported.Length);
                Assert.Contains(span1, exported);
                Assert.Contains(span2, exported);
            }
        }

        [Fact]
        public void ExporterIsSlowerThanDelay()
        {
            var exportStartTimes = new List<long>();
            var exportEndTimes = new List<long>();
            var spanExporter = new TestExporter(_ =>
            {
                exportStartTimes.Add(Stopwatch.GetTimestamp());
                Thread.Sleep(50);
                exportEndTimes.Add(Stopwatch.GetTimestamp());
            });

            using (var spanProcessor = new BatchingSpanProcessor(spanExporter, 128, TimeSpan.FromMilliseconds(30), 2))
            {
                var spans = new List<Span>();
                for (int i = 0; i < 20; i++)
                {
                    spans.Add(CreateSampledEndedSpan(i.ToString(), spanProcessor));
                }

                var exported = WaitForSpans(spanExporter, 20, TimeSpan.FromSeconds(2));

                Assert.Equal(spans.Count, exported.Length);
                Assert.InRange(exportStartTimes.Count, 10, 20);

                for (int i = 1; i < exportStartTimes.Count - 1; i++)
                {
                    Assert.InRange(exportStartTimes[i], exportEndTimes[i - 1] + 1, exportStartTimes[i + 1] - 1);
                }
            }
        }

        [Fact]
        public void AddSpanAfterQueueIsExhausted()
        {
            int exportCalledCount = 0;
            var spanExporter = new TestExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using (var spanProcessor = new BatchingSpanProcessor(spanExporter, 1, TimeSpan.FromMilliseconds(100), 1))
            {
                var spans = new List<Span>();
                for (int i = 0; i < 20; i++)
                {
                    spans.Add(CreateSampledEndedSpan(i.ToString(), spanProcessor));
                }

                var exported = WaitForSpans(spanExporter, 1, DefaultTimeout);

                Assert.Equal(1, exportCalledCount);
                Assert.InRange(exported.Length, 1, 2);
                Assert.Contains(spans.First(), exported);
            }
        }

        [Fact]
        public void ExportMoreSpansThanTheMaxBatchSize()
        {
            int exportCalledCount = 0;
            var spanExporter = new TestExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using (var spanProcessor = new BatchingSpanProcessor(spanExporter, 128, DefaultDelay, 3))
            {
                var span1 = CreateSampledEndedSpan(SpanName1, spanProcessor);
                var span2 = CreateSampledEndedSpan(SpanName1, spanProcessor);
                var span3 = CreateSampledEndedSpan(SpanName1, spanProcessor);
                var span4 = CreateSampledEndedSpan(SpanName1, spanProcessor);
                var span5 = CreateSampledEndedSpan(SpanName1, spanProcessor);
                var span6 = CreateSampledEndedSpan(SpanName1, spanProcessor);

                var exported = WaitForSpans(spanExporter, 6, DefaultTimeout);
                Assert.InRange(exportCalledCount, 2, 6);

                Assert.Equal(6, exported.Count());
                Assert.Contains(span1, exported);
                Assert.Contains(span2, exported);
                Assert.Contains(span3, exported);
                Assert.Contains(span4, exported);
                Assert.Contains(span5, exported);
                Assert.Contains(span6, exported);
            }
        }


        [Fact]
        public void ExportNotSampledSpans()
        {
            int exportCalledCount = 0;
            var spanExporter = new TestExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using (var spanProcessor = new BatchingSpanProcessor(spanExporter, 128, DefaultDelay, 3))
            {
                var span1 = CreateNotSampledEndedSpan(SpanName1, spanProcessor);
                var span2 = CreateSampledEndedSpan(SpanName2, spanProcessor);
                // Spans are recorded and exported in the same order as they are ended, we test that a non
                // sampled span is not exported by creating and ending a sampled span after a non sampled span
                // and checking that the first exported span is the sampled span (the non sampled did not get
                // exported).
                var exported = WaitForSpans(spanExporter, 1, DefaultTimeout);
                Assert.Equal(1, exportCalledCount);

                // Need to check this because otherwise the variable span1 is unused, other option is to not
                // have a span1 variable.
                Assert.Single(exported);
                Assert.Contains(span2, exported);
            }
        }

        [Fact]
        public void ProcessorDoesNotBlockOnExporter()
        { 
            var resetEvent = new ManualResetEvent(false);
            var spanExporter = new TestExporter(_ => resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            using (var factory = TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p
                    .SetExporter(spanExporter)
                    .SetExportingProcessor(e => new BatchingSpanProcessor(e, 128, DefaultDelay, 128)))))
            {
                var tracer = factory.GetTracer(null);

                var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
                var span = (Span)tracer.StartSpan("foo", context);

                // does not block
                var sw = Stopwatch.StartNew();
                span.End();
                sw.Stop();

                Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

                resetEvent.Set();

                var exported = WaitForSpans(spanExporter, 1, DefaultTimeout);

                Assert.Single(exported);
            }
        }

        [Fact]
        public async Task ShutdownOnNotEmptyQueueFullFlush()
        {
            const int batchSize = 2;
            int exportCalledCount = 0;
            var spanExporter = new TestExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using (var spanProcessor =
                new BatchingSpanProcessor(spanExporter, 128, TimeSpan.FromMilliseconds(100), batchSize))
            {
                var spans = new List<Span>();
                for (int i = 0; i < 100; i++)
                {
                    spans.Add(CreateSampledEndedSpan(i.ToString(), spanProcessor));
                }

                Assert.True(spanExporter.ExportedSpans.Length < spans.Count);
                using (var cts = new CancellationTokenSource(DefaultTimeout))
                {
                    await spanProcessor.ShutdownAsync(cts.Token);
                }

                Assert.True(spanExporter.WasShutDown);
                Assert.Equal(spans.Count, spanExporter.ExportedSpans.Length);
                Assert.InRange(exportCalledCount, spans.Count / batchSize, spans.Count);
            }
        }

        [Fact]
        public async Task ShutdownOnNotEmptyQueueNotFullFlush()
        {
            const int batchSize = 2;
            int exportCalledCount = 0;

            // we'll need about 1.5 sec to export all spans
            // we export 100 spans in batches of 2, each export takes 30ms, in one thread 
            var spanExporter = new TestExporter(_ =>
            {
                Interlocked.Increment(ref exportCalledCount);
                Thread.Sleep(30);
            });

            using (var spanProcessor =
                new BatchingSpanProcessor(spanExporter, 128, TimeSpan.FromMilliseconds(100), batchSize))
            {
                var spans = new List<Span>();
                for (int i = 0; i < 100; i++)
                {
                    spans.Add(CreateSampledEndedSpan(i.ToString(), spanProcessor));
                }

                Assert.True(spanExporter.ExportedSpans.Length < spans.Count);

                // we won't bs able to export all before cancellation will fire
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
                {
                    await spanProcessor.ShutdownAsync(cts.Token);
                }

                var exportedCount = spanExporter.ExportedSpans.Length;
                Assert.True(exportedCount < spans.Count);
            }
        }

        [Fact]
        public void DisposeFlushes()
        {
            const int batchSize = 2;
            int exportCalledCount = 0;
            var spanExporter = new TestExporter(_ => Interlocked.Increment(ref exportCalledCount));
            var spans = new List<Span>();
            using (var spanProcessor = new BatchingSpanProcessor(spanExporter, 128, TimeSpan.FromMilliseconds(100), batchSize))
            {
                for (int i = 0; i < 100; i++)
                {
                    spans.Add(CreateSampledEndedSpan(i.ToString(), spanProcessor));
                }
                Assert.True(spanExporter.ExportedSpans.Length < spans.Count);
            }
            Assert.True(spanExporter.WasShutDown);
            Assert.Equal(spans.Count, spanExporter.ExportedSpans.Length);
            Assert.Equal(spans.Count / batchSize, exportCalledCount);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private Span[] WaitForSpans(TestExporter exporter, int spanCount, TimeSpan timeout)
        {
            Assert.True(
                SpinWait.SpinUntil(() =>
                {
                    if (exporter.ExportedSpans.Length >= spanCount)
                    {
                        return true;
                    }

                    Thread.Sleep(10);
                    return false;
                }, timeout),
                $"Expected at least {spanCount}, got {exporter.ExportedSpans.Length}");

            return exporter.ExportedSpans;
        }
    }
}
