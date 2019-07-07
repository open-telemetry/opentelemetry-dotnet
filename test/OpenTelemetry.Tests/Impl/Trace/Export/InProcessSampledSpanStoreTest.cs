// <copyright file="InProcessSampledSpanStoreTest.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Utils;
    using Xunit;

    public class InProcessSampledSpanStoreTest : IDisposable
    {
        private const string RegisteredSpanName = "MySpanName/1";
        private const string NotRegisteredSpanName = "MySpanName/2";

        private readonly ActivitySpanId parentSpanId;
        private readonly SpanOptions recordSpanOptions = SpanOptions.RecordEvents;
        private TimeSpan interval = TimeSpan.FromMilliseconds(0);
        private readonly DateTimeOffset startTime = DateTimeOffset.Now;
        private readonly Timestamp timestamp;
        private readonly Timer timestampConverter;

        private readonly InProcessSampledSpanStore sampleStore = new InProcessSampledSpanStore(new SimpleEventQueue());

        private readonly IStartEndHandler startEndHandler;


        public InProcessSampledSpanStoreTest()
        {
            timestamp = Timestamp.FromDateTimeOffset(startTime);
            timestampConverter = Timer.StartNew(startTime, () => interval);
            parentSpanId = ActivitySpanId.CreateRandom();
            startEndHandler = new TestStartEndHandler(sampleStore);
            sampleStore.RegisterSpanNamesForCollection(new List<string>() { RegisteredSpanName });
        }

        [Fact]
        public void AddSpansWithRegisteredNamesInAllLatencyBuckets()
        {
            AddSpanNameToAllLatencyBuckets(RegisteredSpanName);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            var latencyBucketsSummaries = perSpanNameSummary[RegisteredSpanName].NumbersOfLatencySampledSpans;
            Assert.Equal(LatencyBucketBoundaries.Values.Count, latencyBucketsSummaries.Count);
            foreach (var it in latencyBucketsSummaries)
            {
                Assert.Equal(2, it.Value);
            }
        }

        [Fact]
        public void AddSpansWithoutRegisteredNamesInAllLatencyBuckets()
        {
            AddSpanNameToAllLatencyBuckets(NotRegisteredSpanName);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            Assert.False(perSpanNameSummary.ContainsKey(NotRegisteredSpanName));
        }

        [Fact]
        public void RegisterUnregisterAndListSpanNames()
        {
            Assert.Contains(RegisteredSpanName, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(1, sampleStore.RegisteredSpanNamesForCollection.Count);

            sampleStore.RegisterSpanNamesForCollection(new List<string>() { NotRegisteredSpanName });

            Assert.Contains(RegisteredSpanName,  sampleStore.RegisteredSpanNamesForCollection);
            Assert.Contains(NotRegisteredSpanName, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(2, sampleStore.RegisteredSpanNamesForCollection.Count);

            sampleStore.UnregisterSpanNamesForCollection(new List<string>() { NotRegisteredSpanName });

            Assert.Contains(RegisteredSpanName, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(1, sampleStore.RegisteredSpanNamesForCollection.Count);
        }

        [Fact]
        public void RegisterSpanNamesViaSpanBuilderOption()
        {
            Assert.Contains(RegisteredSpanName, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(1, sampleStore.RegisteredSpanNamesForCollection.Count);

            var span = CreateSampledSpan(NotRegisteredSpanName);
            span.IsSampleToLocalSpanStore = true;
            span.End();

            Assert.Contains(RegisteredSpanName, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Contains(NotRegisteredSpanName, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(2, sampleStore.RegisteredSpanNamesForCollection.Count);

        }

        [Fact]
        public void AddSpansWithRegisteredNamesInAllErrorBuckets()
        {
            AddSpanNameToAllErrorBuckets(RegisteredSpanName);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            var errorBucketsSummaries = perSpanNameSummary[RegisteredSpanName].NumbersOfErrorSampledSpans;
            var ccCount = Enum.GetValues(typeof(CanonicalCode)).Cast<CanonicalCode>().Count();
            Assert.Equal(ccCount - 1, errorBucketsSummaries.Count);
            foreach (var it in errorBucketsSummaries)
            {
                Assert.Equal(2, it.Value);
            }
        }

        [Fact]
        public void AddSpansWithoutRegisteredNamesInAllErrorBuckets()
        {
            AddSpanNameToAllErrorBuckets(NotRegisteredSpanName);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            Assert.False(perSpanNameSummary.ContainsKey(NotRegisteredSpanName));
        }

        [Fact]
        public void GetErrorSampledSpans()
        {
            var span = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);
            span.Status = Status.Cancelled;
            span.End();

            var samples =
                sampleStore.GetErrorSampledSpans(
                    SampledSpanStoreErrorFilter.Create(RegisteredSpanName, CanonicalCode.Cancelled, 0));
            Assert.Single(samples);
            Assert.Contains(span.ToSpanData(), samples);
        }

        [Fact]
        public void GetErrorSampledSpans_MaxSpansToReturn()
        {
            var span1 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);
            span1.Status = Status.Cancelled;
            span1.End();

            // Advance time to allow other spans to be sampled.
            interval += TimeSpan.FromSeconds(5);
            var span2 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);
            span2.Status = Status.Cancelled;
            span2.End();

            var samples =
                sampleStore.GetErrorSampledSpans(
                    SampledSpanStoreErrorFilter.Create(RegisteredSpanName, CanonicalCode.Cancelled, 1));
            Assert.Single(samples);
            // No order guaranteed so one of the spans should be in the list.
            Assert.True(samples.Contains(span1.ToSpanData()) || samples.Contains(span2.ToSpanData()));
        }

        [Fact]
        public void GetErrorSampledSpans_NullCode()
        {
            var span1 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);

            span1.Status = Status.Cancelled;;
            span1.End();

            var span2 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);
            span2.Status = Status.Unknown;
            span2.End();

            var samples =
                sampleStore.GetErrorSampledSpans(SampledSpanStoreErrorFilter.Create(RegisteredSpanName, null, 0));
            Assert.Equal(2, samples.Count());
            Assert.Contains(span1.ToSpanData(), samples);
            Assert.Contains(span2.ToSpanData(), samples);
        }

        [Fact]
        public void GetErrorSampledSpans_NullCode_MaxSpansToReturn()
        {
            var span1 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);
            span1.Status = Status.Cancelled;
            span1.End();
            var span2 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(10);
            span2.Status = Status.Unknown;
            span2.End();

            var samples =
                sampleStore.GetErrorSampledSpans(SampledSpanStoreErrorFilter.Create(RegisteredSpanName, null, 1));
            Assert.Single(samples);
            Assert.True(samples.Contains(span1.ToSpanData()) || samples.Contains(span2.ToSpanData()));
        }

        [Fact]
        public void GetLatencySampledSpans()
        {
            var span = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        RegisteredSpanName,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(250),
                        0));
            Assert.Single(samples);
            Assert.Contains(span.ToSpanData(), samples);
        }

        [Fact]
        public void GetLatencySampledSpans_ExclusiveUpperBound()
        {
            var span = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        RegisteredSpanName,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(200),
                        0));
            Assert.Empty(samples);
        }

        [Fact]
        public void GetLatencySampledSpans_InclusiveLowerBound()
        {
            var span = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        RegisteredSpanName,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(250),
                        0));
            Assert.Single(samples);
            Assert.Contains(span.ToSpanData(), samples);
        }

        [Fact]
        public void GetLatencySampledSpans_QueryBetweenMultipleBuckets()
        {
            var span1 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span1.End();
            // Advance time to allow other spans to be sampled.
            interval += TimeSpan.FromSeconds(5);
            var span2 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(2000); // 200 microseconds
            span2.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        RegisteredSpanName,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(2500),
                        0));
            Assert.Equal(2, samples.Count());
            Assert.Contains(span1.ToSpanData(), samples);
            Assert.Contains(span2.ToSpanData(), samples);
        }

        [Fact]
        public void GetLatencySampledSpans_MaxSpansToReturn()
        {
            var span1 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span1.End();
            // Advance time to allow other spans to be sampled.
            interval += TimeSpan.FromSeconds(5);
            var span2 = CreateSampledSpan(RegisteredSpanName) as Span;
            interval += TimeSpan.FromTicks(2000); // 200 microseconds
            span2.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        RegisteredSpanName,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(2500),
                        1));
            Assert.Single(samples);
            Assert.Contains(span1.ToSpanData(), samples);
        }

        [Fact]
        public void IgnoreNegativeSpanLatency()
        {
            var span = CreateSampledSpan(RegisteredSpanName) as Span;
            interval -= TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(RegisteredSpanName, TimeSpan.Zero, TimeSpan.MaxValue, 0));
            Assert.Empty(samples);
        }

        private Span CreateSampledSpan(string spanName)
        {
            var activity = new Activity(spanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            return (Span)Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordSpanOptions,
                spanName,
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                timestampConverter);
        }

        private Span CreateNotSampledSpan(string spanName)
        {
            var activity = new Activity(spanName).Start();
            activity.ActivityTraceFlags = ActivityTraceFlags.None;

            return (Span)Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordSpanOptions,
                spanName,
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                timestampConverter);
        }

        private void AddSpanNameToAllLatencyBuckets(string spanName)
        {
            foreach (LatencyBucketBoundaries boundaries in LatencyBucketBoundaries.Values)
            {
                ISpan sampledSpan = CreateSampledSpan(spanName);
                ISpan notSampledSpan = CreateNotSampledSpan(spanName);
                interval += boundaries.LatencyLower;
                sampledSpan.End();
                notSampledSpan.End();
            }
        }

        private void AddSpanNameToAllErrorBuckets(String spanName)
        {
            foreach (var code in Enum.GetValues(typeof(CanonicalCode)).Cast<CanonicalCode>())
            {
                if (code != CanonicalCode.Ok)
                {
                    var sampledSpan = CreateSampledSpan(spanName);
                    var notSampledSpan = CreateNotSampledSpan(spanName);
                    interval += TimeSpan.FromTicks(10);

                    sampledSpan.Status = code.ToStatus();
                    notSampledSpan.Status = code.ToStatus();
                    sampledSpan.End();
                    notSampledSpan.End();
                    sampledSpan.Activity.Stop();
                    notSampledSpan.Activity.Stop();
                }
            }
        }

        class TestStartEndHandler : IStartEndHandler
        {
            readonly InProcessSampledSpanStore sampleStore;

            public TestStartEndHandler(InProcessSampledSpanStore store)
            {
                sampleStore = store;
            }

            public void OnStart(ISpan span)
            {
                // Do nothing.
            }

            public void OnEnd(ISpan span)
            {
                sampleStore.ConsiderForSampling(span);
            }
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
