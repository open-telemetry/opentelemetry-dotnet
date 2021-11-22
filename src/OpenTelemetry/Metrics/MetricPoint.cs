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
        private readonly AggregationType aggType;
        private readonly HistogramMeasurements histogramMeasurements;
        private readonly object lockObjectForHistogram;
        private long longVal;
        private long lastLongSum;
        private double doubleVal;
        private double lastDoubleSum;

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
            this.LongValue = default;
            this.longVal = default;
            this.lastLongSum = default;
            this.DoubleValue = default;
            this.doubleVal = default;
            this.lastDoubleSum = default;
            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (this.aggType == AggregationType.Histogram)
            {
                this.histogramMeasurements = new HistogramMeasurements(histogramBounds);
                this.lockObjectForHistogram = new object();
            }
            else if (this.aggType == AggregationType.HistogramSumCount)
            {
                this.histogramMeasurements = new HistogramMeasurements(null);
                this.lockObjectForHistogram = new object();
            }
            else
            {
                this.histogramMeasurements = null;
                this.lockObjectForHistogram = null;
            }
        }

        /// <summary>
        /// Gets the tags associated with the metric point.
        /// </summary>
        public ReadOnlyTagCollection Tags { get; }

        public DateTimeOffset StartTime { get; internal set; }

        public DateTimeOffset EndTime { get; internal set; }

        public long LongValue { get; internal set; }

        public double DoubleValue { get; internal set; }

        internal MetricPointStatus MetricPointStatus { get; private set; }

        public long[] GetBucketCounts()
        {
            if (this.aggType == AggregationType.Histogram)
            {
                return this.histogramMeasurements.AggregatedBucketCounts;
            }
            else
            {
                throw new InvalidOperationException($"{nameof(this.GetBucketCounts)} is not supported for this metric type.");
            }
        }

        public double[] GetExplicitBounds()
        {
            if (this.aggType == AggregationType.Histogram)
            {
                return this.histogramMeasurements.ExplicitBounds;
            }
            else
            {
                throw new InvalidOperationException($"{nameof(this.GetExplicitBounds)} is not supported for this metric type.");
            }
        }

        public long GetHistogramCount()
        {
            if (this.aggType == AggregationType.Histogram || this.aggType == AggregationType.HistogramSumCount)
            {
                return this.histogramMeasurements.Count;
            }
            else
            {
                throw new InvalidOperationException($"{nameof(this.GetHistogramCount)} is not supported for this metric type.");
            }
        }

        public double GetHistogramSum()
        {
            if (this.aggType == AggregationType.Histogram || this.aggType == AggregationType.HistogramSumCount)
            {
                return this.histogramMeasurements.Sum;
            }
            else
            {
                throw new InvalidOperationException($"{nameof(this.GetHistogramSum)} is not supported for this metric type.");
            }
        }

        internal void Update(long number)
        {
            switch (this.aggType)
            {
                case AggregationType.LongSumIncomingDelta:
                    {
                        Interlocked.Add(ref this.longVal, number);
                        break;
                    }

                case AggregationType.LongSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.longVal, number);
                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        Interlocked.Exchange(ref this.longVal, number);
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
                            initValue = this.doubleVal;
                            newValue = initValue + number;
                        }
                        while (initValue != Interlocked.CompareExchange(ref this.doubleVal, newValue, initValue));
                        break;
                    }

                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.doubleVal, number);
                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        Interlocked.Exchange(ref this.doubleVal, number);
                        break;
                    }

                case AggregationType.Histogram:
                    {
                        int i;
                        for (i = 0; i < this.histogramMeasurements.ExplicitBounds.Length; i++)
                        {
                            // Upper bound is inclusive
                            if (number <= this.histogramMeasurements.ExplicitBounds[i])
                            {
                                break;
                            }
                        }

                        lock (this.lockObjectForHistogram)
                        {
                            this.histogramMeasurements.CountVal++;
                            this.histogramMeasurements.SumVal += number;
                            this.histogramMeasurements.BucketCounts[i]++;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        lock (this.lockObjectForHistogram)
                        {
                            this.histogramMeasurements.CountVal++;
                            this.histogramMeasurements.SumVal += number;
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
                            long initValue = Interlocked.Read(ref this.longVal);
                            this.LongValue = initValue - this.lastLongSum;
                            this.lastLongSum = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.Read(ref this.longVal))
                            {
                                this.MetricPointStatus = MetricPointStatus.CollectPending;
                            }
                        }
                        else
                        {
                            this.LongValue = Interlocked.Read(ref this.longVal);
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
                            double initValue = Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity);
                            this.DoubleValue = initValue - this.lastDoubleSum;
                            this.lastDoubleSum = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity))
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
                            this.DoubleValue = Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity);
                        }

                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        this.LongValue = Interlocked.Read(ref this.longVal);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.LongValue != Interlocked.Read(ref this.longVal))
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
                        this.DoubleValue = Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.DoubleValue != Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }

                        break;
                    }

                case AggregationType.Histogram:
                    {
                        lock (this.lockObjectForHistogram)
                        {
                            this.histogramMeasurements.Count = this.histogramMeasurements.CountVal;
                            this.histogramMeasurements.Sum = this.histogramMeasurements.SumVal;
                            if (outputDelta)
                            {
                                this.histogramMeasurements.CountVal = 0;
                                this.histogramMeasurements.SumVal = 0;
                            }

                            for (int i = 0; i < this.histogramMeasurements.BucketCounts.Length; i++)
                            {
                                this.histogramMeasurements.AggregatedBucketCounts[i] = this.histogramMeasurements.BucketCounts[i];
                                if (outputDelta)
                                {
                                    this.histogramMeasurements.BucketCounts[i] = 0;
                                }
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        lock (this.lockObjectForHistogram)
                        {
                            this.histogramMeasurements.Count = this.histogramMeasurements.CountVal;
                            this.histogramMeasurements.Sum = this.histogramMeasurements.SumVal;
                            if (outputDelta)
                            {
                                this.histogramMeasurements.CountVal = 0;
                                this.histogramMeasurements.SumVal = 0;
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }
            }
        }
    }
}
