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
using System.Threading;

namespace OpenTelemetry.Metrics
{
    public struct MetricPoint
    {
        private long longVal;
        private long lastLongSum;
        private double doubleVal;
        private double lastDoubleSum;
        private object lockObject;
        private long[] bucketCounts;

        internal MetricPoint(
            AggregationType aggType,
            DateTimeOffset startTime,
            string[] keys,
            object[] values,
            double[] histogramBounds)
        {
            this.AggType = aggType;
            this.StartTime = startTime;
            this.Keys = keys;
            this.Values = values;
            this.EndTime = default;
            this.LongValue = default;
            this.longVal = default;
            this.lastLongSum = default;
            this.DoubleValue = default;
            this.doubleVal = default;
            this.lastDoubleSum = default;
            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (this.AggType == AggregationType.Histogram)
            {
                this.ExplicitBounds = histogramBounds;
                this.BucketCounts = new long[this.ExplicitBounds.Length + 1];
                this.bucketCounts = new long[this.ExplicitBounds.Length + 1];
                this.lockObject = new object();
            }
            else if (this.AggType == AggregationType.HistogramSumCount)
            {
                this.ExplicitBounds = null;
                this.BucketCounts = null;
                this.bucketCounts = null;
                this.lockObject = new object();
            }
            else
            {
                this.ExplicitBounds = null;
                this.BucketCounts = null;
                this.bucketCounts = null;
                this.lockObject = null;
            }
        }

        public string[] Keys { get; internal set; }

        public object[] Values { get; internal set; }

        public DateTimeOffset StartTime { get; internal set; }

        public DateTimeOffset EndTime { get; internal set; }

        public long LongValue { get; internal set; }

        public double DoubleValue { get; internal set; }

        public long[] BucketCounts { get; internal set; }

        public double[] ExplicitBounds { get; internal set; }

        internal MetricPointStatus MetricPointStatus { get; private set; }

        private readonly AggregationType AggType { get; }

        internal void Update(long number)
        {
            switch (this.AggType)
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
            switch (this.AggType)
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
                        for (i = 0; i < this.ExplicitBounds.Length; i++)
                        {
                            // Upper bound is inclusive
                            if (number <= this.ExplicitBounds[i])
                            {
                                break;
                            }
                        }

                        lock (this.lockObject)
                        {
                            this.longVal++;
                            this.doubleVal += number;
                            this.bucketCounts[i]++;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        lock (this.lockObject)
                        {
                            this.longVal++;
                            this.doubleVal += number;
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
            switch (this.AggType)
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
                        lock (this.lockObject)
                        {
                            this.LongValue = this.longVal;
                            this.DoubleValue = this.doubleVal;
                            if (outputDelta)
                            {
                                this.longVal = 0;
                                this.doubleVal = 0;
                            }

                            for (int i = 0; i < this.bucketCounts.Length; i++)
                            {
                                this.BucketCounts[i] = this.bucketCounts[i];
                                if (outputDelta)
                                {
                                    this.bucketCounts[i] = 0;
                                }
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        lock (this.lockObject)
                        {
                            this.LongValue = this.longVal;
                            this.DoubleValue = this.doubleVal;
                            if (outputDelta)
                            {
                                this.longVal = 0;
                                this.doubleVal = 0;
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }
            }
        }
    }
}
