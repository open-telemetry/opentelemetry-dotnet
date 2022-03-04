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
    /// <summary>
    /// Represents a metric data point.
    /// </summary>
    public struct MetricPoint
    {
        private readonly AggregationType aggType;

        private readonly HistogramBuckets histogramBuckets;

        // Represents temporality adjusted "value" for double/long metric types or "count" when histogram
        private MetricPointValueStorage runningValue;

        // Represents either "value" for double/long metric types or "count" when histogram
        private MetricPointValueStorage snapshotValue;

        private MetricPointValueStorage deltaLastValue;

        internal MetricPoint(
            AggregationType aggType,
            DateTimeOffset startTime,
            string[] keys,
            object[] values,
            double[] histogramExplicitBounds)
        {
            Debug.Assert((keys?.Length ?? 0) == (values?.Length ?? 0), "Key and value array lengths did not match.");
            Debug.Assert(histogramExplicitBounds != null, "Histogram explicit Bounds was null.");

            this.aggType = aggType;
            this.StartTime = startTime;
            this.Tags = new ReadOnlyTagCollection(keys, values);
            this.EndTime = default;
            this.runningValue = default;
            this.snapshotValue = default;
            this.deltaLastValue = default;
            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (this.aggType == AggregationType.Histogram)
            {
                this.histogramBuckets = new HistogramBuckets(histogramExplicitBounds);
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
        public ReadOnlyTagCollection Tags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the start time associated with the metric point.
        /// </summary>
        public DateTimeOffset StartTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal set;
        }

        /// <summary>
        /// Gets the end time associated with the metric point.
        /// </summary>
        public DateTimeOffset EndTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal set;
        }

        internal MetricPointStatus MetricPointStatus
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        /// <summary>
        /// Gets the sum long value associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.LongSum"/> metric type.
        /// </remarks>
        /// <returns>Long sum value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetSumLong()
        {
            if (this.aggType != AggregationType.LongSumIncomingDelta && this.aggType != AggregationType.LongSumIncomingCumulative)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetSumLong));
            }

            return this.snapshotValue.AsLong;
        }

        /// <summary>
        /// Gets the sum double value associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.DoubleSum"/> metric type.
        /// </remarks>
        /// <returns>Double sum value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetSumDouble()
        {
            if (this.aggType != AggregationType.DoubleSumIncomingDelta && this.aggType != AggregationType.DoubleSumIncomingCumulative)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetSumDouble));
            }

            return this.snapshotValue.AsDouble;
        }

        /// <summary>
        /// Gets the last long value of the gauge associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.LongGauge"/> metric type.
        /// </remarks>
        /// <returns>Long gauge value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetGaugeLastValueLong()
        {
            if (this.aggType != AggregationType.LongGauge)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetGaugeLastValueLong));
            }

            return this.snapshotValue.AsLong;
        }

        /// <summary>
        /// Gets the last double value of the gauge associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.DoubleGauge"/> metric type.
        /// </remarks>
        /// <returns>Double gauge value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetGaugeLastValueDouble()
        {
            if (this.aggType != AggregationType.DoubleGauge)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetGaugeLastValueDouble));
            }

            return this.snapshotValue.AsDouble;
        }

        /// <summary>
        /// Gets the count value of the histogram associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.Histogram"/> metric type.
        /// </remarks>
        /// <returns>Count value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHistogramCount()
        {
            if (this.aggType != AggregationType.Histogram && this.aggType != AggregationType.HistogramSumCount)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramCount));
            }

            return this.snapshotValue.AsLong;
        }

        /// <summary>
        /// Gets the sum value of the histogram associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.Histogram"/> metric type.
        /// </remarks>
        /// <returns>Sum value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetHistogramSum()
        {
            if (this.aggType != AggregationType.Histogram && this.aggType != AggregationType.HistogramSumCount)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramSum));
            }

            return this.histogramBuckets.SnapshotSum;
        }

        /// <summary>
        /// Gets the buckets of the histogram associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.Histogram"/> metric type.
        /// </remarks>
        /// <returns><see cref="HistogramBuckets"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HistogramBuckets GetHistogramBuckets()
        {
            if (this.aggType != AggregationType.Histogram && this.aggType != AggregationType.HistogramSumCount)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramBuckets));
            }

            return this.histogramBuckets;
        }

        internal void Update(long number)
        {
            switch (this.aggType)
            {
                case AggregationType.LongSumIncomingDelta:
                    {
                        Interlocked.Add(ref this.runningValue.AsLong, number);
                        break;
                    }

                case AggregationType.LongSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.runningValue.AsLong, number);
                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        Interlocked.Exchange(ref this.runningValue.AsLong, number);
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
                            initValue = this.runningValue.AsDouble;
                            newValue = initValue + number;
                        }
                        while (initValue != Interlocked.CompareExchange(ref this.runningValue.AsDouble, newValue, initValue));
                        break;
                    }

                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        Interlocked.Exchange(ref this.runningValue.AsDouble, number);
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

                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                unchecked
                                {
                                    this.runningValue.AsLong++;
                                    this.histogramBuckets.RunningSum += number;
                                    this.histogramBuckets.RunningBucketCounts[i]++;
                                }

                                this.histogramBuckets.IsCriticalSectionOccupied = 0;
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.HistogramSumCount:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                unchecked
                                {
                                    this.runningValue.AsLong++;
                                    this.histogramBuckets.RunningSum += number;
                                }

                                this.histogramBuckets.IsCriticalSectionOccupied = 0;
                                break;
                            }

                            sw.SpinOnce();
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
                            long initValue = Interlocked.Read(ref this.runningValue.AsLong);
                            this.snapshotValue.AsLong = initValue - this.deltaLastValue.AsLong;
                            this.deltaLastValue.AsLong = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.Read(ref this.runningValue.AsLong))
                            {
                                this.MetricPointStatus = MetricPointStatus.CollectPending;
                            }
                        }
                        else
                        {
                            this.snapshotValue.AsLong = Interlocked.Read(ref this.runningValue.AsLong);
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
                            double initValue = Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity);
                            this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                            this.deltaLastValue.AsDouble = initValue;
                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                            // Check again if value got updated, if yes reset status.
                            // This ensures no Updates get Lost.
                            if (initValue != Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity))
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
                            this.snapshotValue.AsDouble = Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity);
                        }

                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        this.snapshotValue.AsLong = Interlocked.Read(ref this.runningValue.AsLong);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.snapshotValue.AsLong != Interlocked.Read(ref this.runningValue.AsLong))
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
                        this.snapshotValue.AsDouble = Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity);
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (this.snapshotValue.AsDouble != Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }

                        break;
                    }

                case AggregationType.Histogram:
                    {
                        lock (this.histogramBuckets.LockObject)
                        {
                            this.snapshotValue.AsLong = this.runningValue.AsLong;
                            this.histogramBuckets.SnapshotSum = this.histogramBuckets.RunningSum;
                            if (outputDelta)
                            {
                                this.runningValue.AsLong = 0;
                                this.histogramBuckets.RunningSum = 0;
                            }

                            for (int i = 0; i < this.histogramBuckets.RunningBucketCounts.Length; i++)
                            {
                                this.histogramBuckets.SnapshotBucketCounts[i] = this.histogramBuckets.RunningBucketCounts[i];
                                if (outputDelta)
                                {
                                    this.histogramBuckets.RunningBucketCounts[i] = 0;
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
                            this.snapshotValue.AsLong = this.runningValue.AsLong;
                            this.histogramBuckets.SnapshotSum = this.histogramBuckets.RunningSum;
                            if (outputDelta)
                            {
                                this.runningValue.AsLong = 0;
                                this.histogramBuckets.RunningSum = 0;
                            }

                            this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                        }

                        break;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowNotSupportedMetricTypeException(string methodName)
        {
            throw new NotSupportedException($"{methodName} is not supported for this metric type.");
        }
    }
}
