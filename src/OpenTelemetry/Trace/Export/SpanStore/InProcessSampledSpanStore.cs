// <copyright file="InProcessSampledSpanStore.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Utils;

    /// <inheritdoc/>
    public sealed class InProcessSampledSpanStore : SampledSpanStoreBase
    {
        private const int NumSamplesPerLatencySamples = 10;
        private const int NumSamplesPerErrorSamples = 5;
        // The total number of canonical codes - 1 (the OK code).
        private const int NumErrorBuckets = 17 - 1; // CanonicalCode.values().length - 1;

        private static readonly int NumLatencyBuckets = LatencyBucketBoundaries.Values.Count;
        private static readonly int MaxPerSpanNameSamples =
            (NumSamplesPerLatencySamples * NumLatencyBuckets)
                + (NumSamplesPerErrorSamples * NumErrorBuckets);

        private static TimeSpan timeBetweenSamples = TimeSpan.FromSeconds(1);

        private readonly IEventQueue eventQueue;
        private readonly Dictionary<string, PerSpanNameSamples> samples;

        internal InProcessSampledSpanStore(IEventQueue eventQueue)
        {
            this.samples = new Dictionary<string, PerSpanNameSamples>();
            this.eventQueue = eventQueue;
        }

        /// <inheritdoc/>
        public override ISampledSpanStoreSummary Summary
        {
            get
            {
                Dictionary<string, ISampledPerSpanNameSummary> ret = new Dictionary<string, ISampledPerSpanNameSummary>();
                lock (this.samples)
                {
                    foreach (var it in this.samples)
                    {
                        ret[it.Key] = SampledPerSpanNameSummary.Create(it.Value.GetNumbersOfLatencySampledSpans(), it.Value.GetNumbersOfErrorSampledSpans());
                    }
                }

                return SampledSpanStoreSummary.Create(ret);
            }
        }

        /// <inheritdoc/>
        public override ISet<string> RegisteredSpanNamesForCollection
        {
            get
            {
                lock (this.samples)
                {
                    return new HashSet<string>(this.samples.Keys);
                }
            }
        }

        /// <inheritdoc/>
        public override void ConsiderForSampling(ISpan ispan)
        {
            if (ispan is SpanBase span)
            {
                lock (this.samples)
                {
                    string spanName = span.Name;
                    if (span.IsSampleToLocalSpanStore && !this.samples.ContainsKey(spanName))
                    {
                        this.samples[spanName] = new PerSpanNameSamples();
                    }

                    this.samples.TryGetValue(spanName, out PerSpanNameSamples perSpanNameSamples);
                    if (perSpanNameSamples != null)
                    {
                        perSpanNameSamples.ConsiderForSampling(span);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<SpanData> GetErrorSampledSpans(ISampledSpanStoreErrorFilter filter)
        {
            int numSpansToReturn = filter.MaxSpansToReturn == 0 ? MaxPerSpanNameSamples : filter.MaxSpansToReturn;
            IEnumerable<SpanBase> spans = Enumerable.Empty<SpanBase>();

            // Try to not keep the lock to much, do the SpanImpl -> SpanData conversion outside the lock.
            lock (this.samples)
            {
                PerSpanNameSamples perSpanNameSamples = this.samples[filter.SpanName];
                if (perSpanNameSamples != null)
                {
                    spans = perSpanNameSamples.GetErrorSamples(filter.CanonicalCode, numSpansToReturn);
                }
            }

            List<SpanData> ret = new List<SpanData>(spans.Count());
            foreach (SpanBase span in spans)
            {
                ret.Add(span.ToSpanData());
            }

            return ret.AsReadOnly();
        }

        /// <inheritdoc/>
        public override IEnumerable<SpanData> GetLatencySampledSpans(ISampledSpanStoreLatencyFilter filter)
        {
            int numSpansToReturn = filter.MaxSpansToReturn == 0 ? MaxPerSpanNameSamples : filter.MaxSpansToReturn;
            IEnumerable<SpanBase> spans = Enumerable.Empty<SpanBase>();

            // Try to not keep the lock to much, do the SpanImpl -> SpanData conversion outside the lock.
            lock (this.samples)
            {
                PerSpanNameSamples perSpanNameSamples = this.samples[filter.SpanName];
                if (perSpanNameSamples != null)
                {
                    spans = perSpanNameSamples.GetLatencySamples(filter.LatencyLower, filter.LatencyUpper, numSpansToReturn);
                }
            }

            List<SpanData> ret = new List<SpanData>(spans.Count());
            foreach (SpanBase span in spans)
            {
                ret.Add(span.ToSpanData());
            }

            return ret.AsReadOnly();
        }

        /// <inheritdoc/>
        public override void RegisterSpanNamesForCollection(IEnumerable<string> spanNames)
        {
            this.eventQueue.Enqueue(new RegisterSpanNameEvent(this, spanNames));
        }

        /// <inheritdoc/>
        public override void UnregisterSpanNamesForCollection(IEnumerable<string> spanNames)
        {
            this.eventQueue.Enqueue(new UnregisterSpanNameEvent(this, spanNames));
        }

        internal void InternalUnregisterSpanNamesForCollection(ICollection<string> spanNames)
        {
            lock (this.samples)
            {
                foreach (string spanName in spanNames)
                {
                    this.samples.Remove(spanName);
                }
            }
        }

        internal void InternaltRegisterSpanNamesForCollection(ICollection<string> spanNames)
        {
            lock (this.samples)
            {
                foreach (string spanName in spanNames)
                {
                    if (!this.samples.ContainsKey(spanName))
                    {
                        this.samples[spanName] = new PerSpanNameSamples();
                    }
                }
            }
        }

        private sealed class Bucket
        {
            private readonly EvictingQueue<SpanBase> sampledSpansQueue;
            private readonly EvictingQueue<SpanBase> notSampledSpansQueue;
            private DateTimeOffset lastSampledTime;
            private DateTimeOffset lastNotSampledTime;

            public Bucket(int numSamples)
            {
                this.sampledSpansQueue = new EvictingQueue<SpanBase>(numSamples);
                this.notSampledSpansQueue = new EvictingQueue<SpanBase>(numSamples);
            }

            public static void GetSamples(
                int maxSpansToReturn, ICollection<SpanBase> output, EvictingQueue<SpanBase> queue)
            {
                SpanBase[] copy = queue.ToArray();

                foreach (SpanBase span in copy)
                {
                    if (output.Count >= maxSpansToReturn)
                    {
                        break;
                    }

                    output.Add(span);
                }
            }

            public static void GetSamplesFilteredByLatency(
                TimeSpan latencyLower,
                TimeSpan latencyUpper,
                int maxSpansToReturn,
                ICollection<SpanBase> output,
                EvictingQueue<SpanBase> queue)
            {
                SpanBase[] copy = queue.ToArray();
                foreach (SpanBase span in copy)
                {
                    if (output.Count >= maxSpansToReturn)
                    {
                        break;
                    }

                    var spanLatency = span.Latency;
                    if (spanLatency >= latencyLower && spanLatency < latencyUpper)
                    {
                        output.Add(span);
                    }
                }
            }

            public void ConsiderForSampling(SpanBase span)
            {
                var spanEndTime = span.EndTime;
                if (span.Context.TraceOptions.IsSampled)
                {
                    // Need to compare by doing the subtraction all the time because in case of an overflow,
                    // this may never sample again (at least for the next ~200 years). No real chance to
                    // overflow two times because that means the process runs for ~200 years.
                    if (spanEndTime - this.lastSampledTime > timeBetweenSamples)
                    {
                        this.sampledSpansQueue.Add(span);
                        this.lastSampledTime = spanEndTime;
                    }
                }
                else
                {
                    // Need to compare by doing the subtraction all the time because in case of an overflow,
                    // this may never sample again (at least for the next ~200 years). No real chance to
                    // overflow two times because that means the process runs for ~200 years.
                    if (spanEndTime - this.lastNotSampledTime > timeBetweenSamples)
                    {
                        this.notSampledSpansQueue.Add(span);
                        this.lastNotSampledTime = spanEndTime;
                    }
                }
            }

            public void GetSamples(int maxSpansToReturn, ICollection<SpanBase> output)
            {
                GetSamples(maxSpansToReturn, output, this.sampledSpansQueue);
                GetSamples(maxSpansToReturn, output, this.notSampledSpansQueue);
            }

            public void GetSamplesFilteredByLatency(
                TimeSpan latencyLower, TimeSpan latencyUpper, int maxSpansToReturn, ICollection<SpanBase> output)
            {
                GetSamplesFilteredByLatency(
                    latencyLower, latencyUpper, maxSpansToReturn, output, this.sampledSpansQueue);
                GetSamplesFilteredByLatency(
                    latencyLower, latencyUpper, maxSpansToReturn, output, this.notSampledSpansQueue);
            }

            public int GetNumSamples()
            {
                return this.sampledSpansQueue.Count + this.notSampledSpansQueue.Count;
            }
        }

        private sealed class PerSpanNameSamples
        {
            private readonly Bucket[] latencyBuckets;
            private readonly Bucket[] errorBuckets;

            public PerSpanNameSamples()
            {
                this.latencyBuckets = new Bucket[NumLatencyBuckets];
                for (int i = 0; i < NumLatencyBuckets; i++)
                {
                    this.latencyBuckets[i] = new Bucket(NumSamplesPerLatencySamples);
                }

                this.errorBuckets = new Bucket[NumErrorBuckets];
                for (int i = 0; i < NumErrorBuckets; i++)
                {
                    this.errorBuckets[i] = new Bucket(NumSamplesPerErrorSamples);
                }
            }

            public Bucket GetLatencyBucket(TimeSpan latency)
            {
                for (int i = 0; i < NumLatencyBuckets; i++)
                {
                    ISampledLatencyBucketBoundaries boundaries = LatencyBucketBoundaries.Values[i];
                    if (latency >= boundaries.LatencyLower
                        && latency < boundaries.LatencyUpper)
                    {
                        return this.latencyBuckets[i];
                    }
                }

                // latencyNs is negative or Long.MAX_VALUE, so this Span can be ignored. This cannot happen
                // in real production because System#nanoTime is monotonic.
                return null;
            }

            public Bucket GetErrorBucket(CanonicalCode code)
            {
                return this.errorBuckets[(int)code - 1];
            }

            public void ConsiderForSampling(SpanBase span)
            {
                Status status = span.Status;

                // Null status means running Span, this should not happen in production, but the library
                // should not crash because of this.
                if (status != null)
                {
                    Bucket bucket =
                        status.IsOk
                            ? this.GetLatencyBucket(span.Latency)
                            : this.GetErrorBucket(status.CanonicalCode);

                    // If unable to find the bucket, ignore this Span.
                    if (bucket != null)
                    {
                        bucket.ConsiderForSampling(span);
                    }
                }
            }

            public IDictionary<ISampledLatencyBucketBoundaries, int> GetNumbersOfLatencySampledSpans()
            {
                IDictionary<ISampledLatencyBucketBoundaries, int> latencyBucketSummaries = new Dictionary<ISampledLatencyBucketBoundaries, int>();
                for (int i = 0; i < NumLatencyBuckets; i++)
                {
                    latencyBucketSummaries[LatencyBucketBoundaries.Values[i]] = this.latencyBuckets[i].GetNumSamples();
                }

                return latencyBucketSummaries;
            }

            public IDictionary<CanonicalCode, int> GetNumbersOfErrorSampledSpans()
            {
                IDictionary<CanonicalCode, int> errorBucketSummaries = new Dictionary<CanonicalCode, int>();
                for (int i = 0; i < NumErrorBuckets; i++)
                {
                    errorBucketSummaries[(CanonicalCode)i + 1] = this.errorBuckets[i].GetNumSamples();
                }

                return errorBucketSummaries;
            }

            public IEnumerable<SpanBase> GetErrorSamples(CanonicalCode? code, int maxSpansToReturn)
            {
                List<SpanBase> output = new List<SpanBase>(maxSpansToReturn);
                if (code.HasValue)
                {
                    this.GetErrorBucket(code.Value).GetSamples(maxSpansToReturn, output);
                }
                else
                {
                    for (int i = 0; i < NumErrorBuckets; i++)
                    {
                        this.errorBuckets[i].GetSamples(maxSpansToReturn, output);
                    }
                }

                return output;
            }

            public IEnumerable<SpanBase> GetLatencySamples(TimeSpan latencyLower, TimeSpan latencyUpper, int maxSpansToReturn)
            {
                List<SpanBase> output = new List<SpanBase>(maxSpansToReturn);
                for (int i = 0; i < NumLatencyBuckets; i++)
                {
                    ISampledLatencyBucketBoundaries boundaries = LatencyBucketBoundaries.Values[i];
                    if (latencyUpper >= boundaries.LatencyLower
                        && latencyLower < boundaries.LatencyUpper)
                    {
                        this.latencyBuckets[i].GetSamplesFilteredByLatency(latencyLower, latencyUpper, maxSpansToReturn, output);
                    }
                }

                return output;
            }
        }

        private sealed class RegisterSpanNameEvent : IEventQueueEntry
        {
            private readonly InProcessSampledSpanStore sampledSpanStore;
            private readonly ICollection<string> spanNames;

            public RegisterSpanNameEvent(InProcessSampledSpanStore sampledSpanStore, IEnumerable<string> spanNames)
            {
                this.sampledSpanStore = sampledSpanStore;
                this.spanNames = new List<string>(spanNames);
            }

            public void Process()
            {
                this.sampledSpanStore.InternaltRegisterSpanNamesForCollection(this.spanNames);
            }
        }

        private sealed class UnregisterSpanNameEvent : IEventQueueEntry
        {
            private readonly InProcessSampledSpanStore sampledSpanStore;
            private readonly ICollection<string> spanNames;

            public UnregisterSpanNameEvent(InProcessSampledSpanStore sampledSpanStore, IEnumerable<string> spanNames)
            {
                this.sampledSpanStore = sampledSpanStore;
                this.spanNames = new List<string>(spanNames);
            }

            public void Process()
            {
                this.sampledSpanStore.InternalUnregisterSpanNamesForCollection(this.spanNames);
            }
        }
    }
}
