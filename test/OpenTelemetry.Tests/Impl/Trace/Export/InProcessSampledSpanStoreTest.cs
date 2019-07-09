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
    using System.Linq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Internal;
    using OpenTelemetry.Utils;
    using Xunit;

    public class InProcessSampledSpanStoreTest
    {
        private static readonly String REGISTERED_SPAN_NAME = "MySpanName/1";
        private static readonly String NOT_REGISTERED_SPAN_NAME = "MySpanName/2";
        private readonly RandomGenerator random = new RandomGenerator(1234);
        private readonly SpanContext sampledSpanContext;

        private readonly SpanContext notSampledSpanContext;

        private readonly SpanId parentSpanId;
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
            sampledSpanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), TraceOptions.Builder().SetIsSampled(true).Build(), Tracestate.Empty);
            notSampledSpanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), TraceOptions.Default, Tracestate.Empty);
            parentSpanId = SpanId.GenerateRandomId(random);
            startEndHandler = new TestStartEndHandler(sampleStore);
            sampleStore.RegisterSpanNamesForCollection(new List<string>() { REGISTERED_SPAN_NAME });
        }



        [Fact]
        public void AddSpansWithRegisteredNamesInAllLatencyBuckets()
        {
            AddSpanNameToAllLatencyBuckets(REGISTERED_SPAN_NAME);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            var latencyBucketsSummaries = perSpanNameSummary[REGISTERED_SPAN_NAME].NumbersOfLatencySampledSpans;
            Assert.Equal(LatencyBucketBoundaries.Values.Count, latencyBucketsSummaries.Count);
            foreach (var it in latencyBucketsSummaries)
            {
                Assert.Equal(2, it.Value);
            }
        }

        [Fact]
        public void AddSpansWithoutRegisteredNamesInAllLatencyBuckets()
        {
            AddSpanNameToAllLatencyBuckets(NOT_REGISTERED_SPAN_NAME);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            Assert.False(perSpanNameSummary.ContainsKey(NOT_REGISTERED_SPAN_NAME));
        }

        [Fact]
        public void RegisterUnregisterAndListSpanNames()
        {
            Assert.Contains(REGISTERED_SPAN_NAME, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(1, sampleStore.RegisteredSpanNamesForCollection.Count);

            sampleStore.RegisterSpanNamesForCollection(new List<string>() { NOT_REGISTERED_SPAN_NAME });

            Assert.Contains(REGISTERED_SPAN_NAME,  sampleStore.RegisteredSpanNamesForCollection);
            Assert.Contains(NOT_REGISTERED_SPAN_NAME, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(2, sampleStore.RegisteredSpanNamesForCollection.Count);

            sampleStore.UnregisterSpanNamesForCollection(new List<string>() { NOT_REGISTERED_SPAN_NAME });

            Assert.Contains(REGISTERED_SPAN_NAME, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(1, sampleStore.RegisteredSpanNamesForCollection.Count);
        }

        [Fact]
        public void RegisterSpanNamesViaSpanBuilderOption()
        {
            Assert.Contains(REGISTERED_SPAN_NAME, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(1, sampleStore.RegisteredSpanNamesForCollection.Count);

            CreateSampledSpan(NOT_REGISTERED_SPAN_NAME).End(EndSpanOptions.Builder().SetSampleToLocalSpanStore(true).Build());

            Assert.Contains(REGISTERED_SPAN_NAME, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Contains(NOT_REGISTERED_SPAN_NAME, sampleStore.RegisteredSpanNamesForCollection);
            Assert.Equal(2, sampleStore.RegisteredSpanNamesForCollection.Count);

        }

        [Fact]
        public void AddSpansWithRegisteredNamesInAllErrorBuckets()
        {
            AddSpanNameToAllErrorBuckets(REGISTERED_SPAN_NAME);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            var errorBucketsSummaries = perSpanNameSummary[REGISTERED_SPAN_NAME].NumbersOfErrorSampledSpans;
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
            AddSpanNameToAllErrorBuckets(NOT_REGISTERED_SPAN_NAME);
            var perSpanNameSummary = sampleStore.Summary.PerSpanNameSummary;
            Assert.Equal(1, perSpanNameSummary.Count);
            Assert.False(perSpanNameSummary.ContainsKey(NOT_REGISTERED_SPAN_NAME));
        }

        [Fact]
        public void GetErrorSampledSpans()
        {
            var span = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
            var samples =
                sampleStore.GetErrorSampledSpans(
                    SampledSpanStoreErrorFilter.Create(REGISTERED_SPAN_NAME, CanonicalCode.Cancelled, 0));
            Assert.Single(samples);
            Assert.Contains(span.ToSpanData(), samples);
        }

        [Fact]
        public void GetErrorSampledSpans_MaxSpansToReturn()
        {
            var span1 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span1.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
            // Advance time to allow other spans to be sampled.
            interval += TimeSpan.FromSeconds(5);
            var span2 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span2.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
            var samples =
                sampleStore.GetErrorSampledSpans(
                    SampledSpanStoreErrorFilter.Create(REGISTERED_SPAN_NAME, CanonicalCode.Cancelled, 1));
            Assert.Single(samples);
            // No order guaranteed so one of the spans should be in the list.
            Assert.True(samples.Contains(span1.ToSpanData()) || samples.Contains(span2.ToSpanData()));
        }

        [Fact]
        public void GetErrorSampledSpans_NullCode()
        {
            var span1 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span1.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
            var span2 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span2.End(EndSpanOptions.Builder().SetStatus(Status.Unknown).Build());
            var samples =
                sampleStore.GetErrorSampledSpans(SampledSpanStoreErrorFilter.Create(REGISTERED_SPAN_NAME, null, 0));
            Assert.Equal(2, samples.Count());
            Assert.Contains(span1.ToSpanData(), samples);
            Assert.Contains(span2.ToSpanData(), samples);
        }

        [Fact]
        public void GetErrorSampledSpans_NullCode_MaxSpansToReturn()
        {
            var span1 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span1.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
            var span2 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(10);
            span2.End(EndSpanOptions.Builder().SetStatus(Status.Unknown).Build());
            var samples =
                sampleStore.GetErrorSampledSpans(SampledSpanStoreErrorFilter.Create(REGISTERED_SPAN_NAME, null, 1));
            Assert.Single(samples);
            Assert.True(samples.Contains(span1.ToSpanData()) || samples.Contains(span2.ToSpanData()));
        }

        [Fact]
        public void GetLatencySampledSpans()
        {
            var span = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        REGISTERED_SPAN_NAME,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(250),
                        0));
            Assert.Single(samples);
            Assert.Contains(span.ToSpanData(), samples);
        }

        [Fact]
        public void GetLatencySampledSpans_ExclusiveUpperBound()
        {
            var span = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        REGISTERED_SPAN_NAME,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(200),
                        0));
            Assert.Empty(samples);
        }

        [Fact]
        public void GetLatencySampledSpans_InclusiveLowerBound()
        {
            var span = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        REGISTERED_SPAN_NAME,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(250),
                        0));
            Assert.Single(samples);
            Assert.Contains(span.ToSpanData(), samples);
        }

        [Fact]
        public void GetLatencySampledSpans_QueryBetweenMultipleBuckets()
        {
            var span1 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span1.End();
            // Advance time to allow other spans to be sampled.
            interval += TimeSpan.FromSeconds(5);
            var span2 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(2000); // 200 microseconds
            span2.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        REGISTERED_SPAN_NAME,
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
            var span1 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(200); // 20 microseconds
            span1.End();
            // Advance time to allow other spans to be sampled.
            interval += TimeSpan.FromSeconds(5);
            var span2 = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval += TimeSpan.FromTicks(2000); // 200 microseconds
            span2.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(
                        REGISTERED_SPAN_NAME,
                        TimeSpan.FromTicks(150),
                        TimeSpan.FromTicks(2500),
                        1));
            Assert.Single(samples);
            Assert.Contains(span1.ToSpanData(), samples);
        }

        [Fact]
        public void IgnoreNegativeSpanLatency()
        {
            var span = CreateSampledSpan(REGISTERED_SPAN_NAME) as Span;
            interval -= TimeSpan.FromTicks(200); // 20 microseconds
            span.End();
            var samples =
                sampleStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create(REGISTERED_SPAN_NAME, TimeSpan.Zero, TimeSpan.MaxValue, 0));
            Assert.Empty(samples);
        }

        private Span CreateSampledSpan(string spanName)
        {
            return (Span)Span.StartSpan(
                sampledSpanContext,
                recordSpanOptions,
                spanName,
                SpanKind.Internal,
                parentSpanId,
                TraceParams.Default,
                startEndHandler,
                timestampConverter);
        }

        private Span CreateNotSampledSpan(string spanName)
        {
            return (Span)Span.StartSpan(
                notSampledSpanContext,
                recordSpanOptions,
                spanName,
                SpanKind.Internal,
                parentSpanId,
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
                    sampledSpan.End(EndSpanOptions.Builder().SetStatus(code.ToStatus()).Build());
                    notSampledSpan.End(EndSpanOptions.Builder().SetStatus(code.ToStatus()).Build());
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
    }
}
