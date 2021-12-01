// <copyright file="MetricPoint.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenTelemetry.Metrics
{
    public struct MetricPoint
    {
        private readonly AggregationType aggType;

        // Represents either "value" for double/long metric types or "count" when histogram
        private MetricPointPrimaryValueStorage primaryValue;

        // Represents either "lastValue" for double/long metric types when delta or pointer to buckets when histogram
        private MetricPointSecondaryValueStorage secondaryValue;

        internal MetricPoint(
            AggregationType aggType,
            DateTimeOffset startTime,
            string[] keys,
            object[] values,
            double[] histogramBounds)
        {
            Debug.Assert((keys?.Length ?? 0) == (values?.Length ?? 0), "Key and value array lengths did not match.");

            this.aggType = aggType;
            this.StartTime = startTime;
            this.Tags = new ReadOnlyTagCollection(keys, values);
            this.EndTime = default;
            this.primaryValue = default;
            this.secondaryValue = default;
            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (this.aggType == AggregationType.Histogram)
            {
                this.secondaryValue.HistogramBuckets = new HistogramBuckets(histogramBounds);
            }
            else if (this.aggType == AggregationType.HistogramSumCount)
            {
                this.secondaryValue.HistogramBuckets = new HistogramBuckets(null);
            }
        }

        /// <summary>
        /// Gets the tags associated with the metric point.
        /// </summary>
        public ReadOnlyTagCollection Tags { get; }

        public DateTimeOffset StartTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal set;
        }

        public DateTimeOffset EndTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal set;
        }

        internal MetricPointStatus MetricPointStatus { get; private set; }

        public long GetSumLong()
        {
            if (this.aggType == AggregationType.LongSumIncomingDelta || this.aggType == AggregationType.LongSumIncomingCumulative)
            {
                return this.primaryValue.Snapshot.AsLong;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetSumLong)} is not supported for this metric type.");
            }
        }

        public double GetSumDouble()
        {
            if (this.aggType == AggregationType.DoubleSumIncomingDelta || this.aggType == AggregationType.DoubleSumIncomingCumulative)
            {
                return this.primaryValue.Snapshot.AsDouble;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetSumDouble)} is not supported for this metric type.");
            }
        }

        public long GetGaugeLastValueLong()
        {
            if (this.aggType == AggregationType.LongGauge)
            {
                return this.primaryValue.Snapshot.AsLong;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetGaugeLastValueLong)} is not supported for this metric type.");
            }
        }

        public double GetGaugeLastValueDouble()
        {
            if (this.aggType == AggregationType.DoubleGauge)
            {
                return this.primaryValue.Snapshot.AsDouble;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetGaugeLastValueDouble)} is not supported for this metric type.");
            }
        }

        public long GetHistogramCount()
        {
            if (this.aggType == AggregationType.Histogram || this.aggType == AggregationType.HistogramSumCount)
            {
                return this.primaryValue.Snapshot.AsLong;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetHistogramCount)} is not supported for this metric type.");
            }
        }

        public double GetHistogramSum()
        {
            if (this.aggType == AggregationType.Histogram || this.aggType == AggregationType.HistogramSumCount)
            {
                return this.secondaryValue.HistogramBuckets.Sum.Snapshot.AsDouble;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetHistogramSum)} is not supported for this metric type.");
            }
        }

        public HistogramBuckets GetHistogramBuckets()
        {
            if (this.aggType == AggregationType.Histogram || this.aggType == AggregationType.HistogramSumCount)
            {
                return this.secondaryValue.HistogramBuckets;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetHistogramBuckets)} is not supported for this metric type.");
            }
        }

        internal void Update(long number)
        {
            switch (this.aggType)
            {
                case AggregationType.LongSumIncomingDelta:
                    {
                        Interlocked.Add(ref this.primaryValue.Current.AsLong, number);
                        break;
                    }

                case AggregationType.LongSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.primaryValue.Current.AsLong, number);
                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        Interlocked.Exchange(ref this.primaryValue.Current.AsLong, number);
                        break;
                    }

                case AggregationType.Histogram:
                case AggregationType.HistogramSumCount:
                    {
                        this.Update((double)number);
                        break;
                    }
            }

            // There is a race with Snapshot:
            // Update() updates the value
            // Snapshot snapshots the value
            // Snapshot sets status to NoCollectPending
            // Update sets status to CollectPending -- this is not right as the Snapshot
            // already included the updated value.
            // In the absence of any new Update call until next Snapshot,
            // this results in exporting an Update even though
            // it had no update.
            // TODO: For Delta, this can be mitigated
            // by ignoring Zero points
            this.MetricPointStatus = MetricPointStatus.CollectPending;
        }

        internal void Update(double number)
        {
            switch (this.aggType)
            {
                case AggregationType.DoubleSumIncomingDelta:
                    {
                        double initValue, newValue;
                        do
                        {
                            initValue = this.primaryValue.Current.AsDouble;
                            newValue = initValue + number;
                        }
                        while (initValue != Interlocked.CompareExchange(ref this.primaryValue.Current.AsDouble, newValue, initValue));
                        break;
                    }

                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.primaryValue.Current.AsDouble, number);
                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        Interlocked.Exchange(ref this.primaryValue.Current.AsDouble, number);
                        break;
                    }

                case AggregationType.Histogram:
                    {
                        var histogramBuckets = this.secondaryValue.HistogramBuckets;

                        int i;
                        for (i = 0; i < histogramBuckets.ExplicitBounds.Length; i++)
                        {
                            // Upper bound is inclusive
                            if (number <= histogramBuckets.ExplicitBounds[i])
                            {
                                break;
                            }
                        }

                        lock (histogramBuckets.LockObject)
                        {
                            this.primaryValue.Current.AsLong++;
                            histogramBuckets.Sum.Current.AsDouble += number;
                            histogramBuckets.CurrentBucketCounts[i]++;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        var histogramBuckets = this.secondaryValue.HistogramBuckets;

                        lock (histogramBuckets.LockObject)
                        {
                            this.primaryValue.Current.AsLong++;
                            histogramBuckets.Sum.Current.AsDouble += number;
                        }

                        break;
                    }
            }

            // There is a race with Snapshot:
            // Update() updates the value
            // Snapshot snapshots the value
            // Snapshot sets status to NoCollectPending
            // Update sets status to CollectPending -- this is not right as the Snapshot
            // already included the updated value.
            // In the absence of any new Update call until next Snapshot,
            // this results in exporting an Update even though
            // it had no update.
            // TODO: For Delta, this can be mitigated
            // by ignoring Zero points
            this.MetricPointStatus = MetricPointStatus.CollectPending;
        }

        internal void TakeSnapshot(bool outputDelta)
        {
            switch (this.aggType)
            {
                case AggregationType.LongSumIncomingDelta:
                case AggregationType.LongSumIncomingCumulative:
                    {
                        if (outputDelta)
                        {
                            MetricPointDeltaState deltaState = this.EnsureDeltaState();

                            long initValue = Interlocked.Read(ref this.primaryValue.Current.AsLong);
                            this.primaryValue.Snapshot.AsLong = initValue - deltaState.LastValue.AsLong;
                            deltaState.LastValue.AsLong = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.Read(ref this.primaryValue.Current.AsLong))
                            {
                                this.MetricPointStatus = MetricPointStatus.CollectPending;
                            }
                        }
                        else
                        {
                            this.primaryValue.Snapshot.AsLong = Interlocked.Read(ref this.primaryValue.Current.AsLong);
                        }

                        break;
                    }

                case AggregationType.DoubleSumIncomingDelta:
                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        if (outputDelta)
                        {
                            MetricPointDeltaState deltaState = this.EnsureDeltaState();

                            // TODO:
                            // Is this thread-safe way to read double?
                            // As long as the value is not -ve infinity,
                            // the exchange (to 0.0) will never occur,
                            // but we get the original value atomically.
                            double initValue = Interlocked.CompareExchange(ref this.primaryValue.Current.AsDouble, 0.0, double.NegativeInfinity);
                            this.primaryValue.Snapshot.AsDouble = initValue - deltaState.LastValue.AsDouble;
                            deltaState.LastValue.AsDouble = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.CompareExchange(ref this.primaryValue.Current.AsDouble, 0.0, double.NegativeInfinity))
                            {
                                this.MetricPointStatus = MetricPointStatus.CollectPending;
                            }
                        }
                        else
                        {
                            // TODO:
                            // Is this thread-safe way to read double?
                            // As long as the value is not -ve infinity,
                            // the exchange (to 0.0) will never occur,
                            // but we get the original value atomically.
                            this.primaryValue.Snapshot.AsDouble = Interlocked.CompareExchange(ref this.primaryValue.Current.AsDouble, 0.0, double.NegativeInfinity);
                        }

                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        this.primaryValue.Snapshot.AsLong = Interlocked.Read(ref this.primaryValue.Current.AsLong);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.primaryValue.Snapshot.AsLong != Interlocked.Read(ref this.primaryValue.Current.AsLong))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }

                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        // TODO:
                        // Is this thread-safe way to read double?
                        // As long as the value is not -ve infinity,
                        // the exchange (to 0.0) will never occur,
                        // but we get the original value atomically.
                        this.primaryValue.Snapshot.AsDouble = Interlocked.CompareExchange(ref this.primaryValue.Current.AsDouble, 0.0, double.NegativeInfinity);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.primaryValue.Snapshot.AsDouble != Interlocked.CompareExchange(ref this.primaryValue.Current.AsDouble, 0.0, double.NegativeInfinity))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }

                        break;
                    }

                case AggregationType.Histogram:
                    {
                        var histogramBuckets = this.secondaryValue.HistogramBuckets;

                        lock (histogramBuckets.LockObject)
                        {
                            this.primaryValue.Snapshot.AsLong = this.primaryValue.Current.AsLong;
                            histogramBuckets.Sum.Snapshot.AsDouble = histogramBuckets.Sum.Current.AsDouble;
                            if (outputDelta)
                            {
                                this.primaryValue.Current.AsLong = 0;
                                histogramBuckets.Sum.Current.AsDouble = 0;
                            }

                            for (int i = 0; i < histogramBuckets.CurrentBucketCounts.Length; i++)
                            {
                                histogramBuckets.SnapshotBucketCounts[i] = histogramBuckets.CurrentBucketCounts[i];
                                if (outputDelta)
                                {
                                    histogramBuckets.CurrentBucketCounts[i] = 0;
                                }
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        var histogramBuckets = this.secondaryValue.HistogramBuckets;

                        lock (histogramBuckets.LockObject)
                        {
                            this.primaryValue.Snapshot.AsLong = this.primaryValue.Current.AsLong;
                            histogramBuckets.Sum.Snapshot.AsDouble = histogramBuckets.Sum.Current.AsDouble;
                            if (outputDelta)
                            {
                                this.primaryValue.Current.AsLong = 0;
                                histogramBuckets.Sum.Current.AsDouble = 0;
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }
            }
        }

        private MetricPointDeltaState EnsureDeltaState()
        {
            var deltaState = this.secondaryValue.DeltaState;
            if (deltaState == null)
            {
                deltaState = new();
                var existing = Interlocked.CompareExchange(ref this.secondaryValue.DeltaState, deltaState, null);
                if (existing != null)
                {
                    return existing;
                }
            }

            return deltaState;
        }
    }
}
