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
using System.Threading;

namespace OpenTelemetry.Metrics
{
    public struct MetricPoint
    {
        internal DateTimeOffset StartTime;
        internal DateTimeOffset EndTime;

        private readonly AggregationType aggType;
        private readonly HistogramBuckets histogramBuckets;

        // Represents either "value" for double/long metric types or "count" when histogram
        private MetricPointValueStorage primaryValue;

        // Represents either "lastValue" for double/long metric types when delta or "sum" when histogram
        private MetricPointValueStorage secondaryValue;

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
                this.histogramBuckets = new HistogramBuckets(histogramBounds);
            }
            else if (this.aggType == AggregationType.HistogramSumCount)
            {
                this.histogramBuckets = new HistogramBuckets(null);
            }
            else
            {
                this.histogramBuckets = null;
            }
        }

        /// <summary>
        /// Gets the tags associated with the metric point.
        /// </summary>
        public ReadOnlyTagCollection Tags { get; }

        internal MetricPointStatus MetricPointStatus { get; private set; }

        public DateTimeOffset GetStartTime() => this.StartTime;

        public DateTimeOffset GetEndTime() => this.EndTime;

        public long GetCounterSumLong()
        {
            if (this.aggType == AggregationType.LongSumIncomingDelta || this.aggType == AggregationType.LongSumIncomingCumulative)
            {
                return this.primaryValue.SnapshotAsLong;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetCounterSumLong)} is not supported for this metric type.");
            }
        }

        public double GetCounterSumDouble()
        {
            if (this.aggType == AggregationType.DoubleSumIncomingDelta || this.aggType == AggregationType.DoubleSumIncomingCumulative)
            {
                return this.primaryValue.SnapshotAsDouble;
            }
            else
            {
                throw new NotSupportedException($"{nameof(this.GetCounterSumDouble)} is not supported for this metric type.");
            }
        }

        public long GetGaugeLastValueLong()
        {
            if (this.aggType == AggregationType.LongGauge)
            {
                return this.primaryValue.SnapshotAsLong;
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
                return this.primaryValue.SnapshotAsDouble;
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
                return this.primaryValue.SnapshotAsLong;
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
                return this.secondaryValue.SnapshotAsDouble;
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
                return this.histogramBuckets;
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
                        Interlocked.Add(ref this.primaryValue.CurrentAsLong, number);
                        break;
                    }

                case AggregationType.LongSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.primaryValue.CurrentAsLong, number);
                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        Interlocked.Exchange(ref this.primaryValue.CurrentAsLong, number);
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
                            initValue = this.primaryValue.CurrentAsDouble;
                            newValue = initValue + number;
                        }
                        while (initValue != Interlocked.CompareExchange(ref this.primaryValue.CurrentAsDouble, newValue, initValue));
                        break;
                    }

                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.primaryValue.CurrentAsDouble, number);
                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        Interlocked.Exchange(ref this.primaryValue.CurrentAsDouble, number);
                        break;
                    }

                case AggregationType.Histogram:
                    {
                        int i;
                        for (i = 0; i < this.histogramBuckets.ExplicitBounds.Length; i++)
                        {
                            // Upper bound is inclusive
                            if (number <= this.histogramBuckets.ExplicitBounds[i])
                            {
                                break;
                            }
                        }

                        lock (this.histogramBuckets.LockObject)
                        {
                            this.primaryValue.CurrentAsLong++;
                            this.secondaryValue.CurrentAsDouble += number;
                            this.histogramBuckets.BucketCounts[i]++;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        lock (this.histogramBuckets.LockObject)
                        {
                            this.primaryValue.CurrentAsLong++;
                            this.secondaryValue.CurrentAsDouble += number;
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
                            long initValue = Interlocked.Read(ref this.primaryValue.CurrentAsLong);
                            this.primaryValue.SnapshotAsLong = initValue - this.secondaryValue.CurrentAsLong;
                            this.secondaryValue.CurrentAsLong = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.Read(ref this.primaryValue.CurrentAsLong))
                            {
                                this.MetricPointStatus = MetricPointStatus.CollectPending;
                            }
                        }
                        else
                        {
                            this.primaryValue.SnapshotAsLong = Interlocked.Read(ref this.primaryValue.CurrentAsLong);
                        }

                        break;
                    }

                case AggregationType.DoubleSumIncomingDelta:
                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        if (outputDelta)
                        {
                            // TODO:
                            // Is this thread-safe way to read double?
                            // As long as the value is not -ve infinity,
                            // the exchange (to 0.0) will never occur,
                            // but we get the original value atomically.
                            double initValue = Interlocked.CompareExchange(ref this.primaryValue.CurrentAsDouble, 0.0, double.NegativeInfinity);
                            this.primaryValue.SnapshotAsDouble = initValue - this.secondaryValue.CurrentAsDouble;
                            this.secondaryValue.CurrentAsDouble = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.CompareExchange(ref this.primaryValue.CurrentAsDouble, 0.0, double.NegativeInfinity))
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
                            this.primaryValue.SnapshotAsDouble = Interlocked.CompareExchange(ref this.primaryValue.CurrentAsDouble, 0.0, double.NegativeInfinity);
                        }

                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        this.primaryValue.SnapshotAsLong = Interlocked.Read(ref this.primaryValue.CurrentAsLong);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.primaryValue.SnapshotAsLong != Interlocked.Read(ref this.primaryValue.CurrentAsLong))
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
                        this.primaryValue.SnapshotAsDouble = Interlocked.CompareExchange(ref this.primaryValue.CurrentAsDouble, 0.0, double.NegativeInfinity);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.primaryValue.SnapshotAsDouble != Interlocked.CompareExchange(ref this.primaryValue.CurrentAsDouble, 0.0, double.NegativeInfinity))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }

                        break;
                    }

                case AggregationType.Histogram:
                    {
                        lock (this.histogramBuckets.LockObject)
                        {
                            this.primaryValue.SnapshotAsLong = this.primaryValue.CurrentAsLong;
                            this.secondaryValue.SnapshotAsDouble = this.secondaryValue.CurrentAsDouble;
                            if (outputDelta)
                            {
                                this.primaryValue.CurrentAsLong = 0;
                                this.secondaryValue.CurrentAsDouble = 0;
                            }

                            for (int i = 0; i < this.histogramBuckets.BucketCounts.Length; i++)
                            {
                                this.histogramBuckets.AggregatedBucketCounts[i] = this.histogramBuckets.BucketCounts[i];
                                if (outputDelta)
                                {
                                    this.histogramBuckets.BucketCounts[i] = 0;
                                }
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        lock (this.histogramBuckets.LockObject)
                        {
                            this.primaryValue.SnapshotAsLong = this.primaryValue.CurrentAsLong;
                            this.secondaryValue.SnapshotAsDouble = this.secondaryValue.CurrentAsDouble;
                            if (outputDelta)
                            {
                                this.primaryValue.CurrentAsLong = 0;
                                this.secondaryValue.CurrentAsDouble = 0;
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }
            }
        }
    }
}
