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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Represents a metric data point.
    /// </summary>
    public struct MetricPoint
    {
        private readonly AggregatorStore aggregatorStore;

        private readonly AggregationType aggType;

        private HistogramBuckets histogramBuckets;

        // Represents temporality adjusted "value" for double/long metric types or "count" when histogram
        private MetricPointValueStorage runningValue;

        // Represents either "value" for double/long metric types or "count" when histogram
        private MetricPointValueStorage snapshotValue;

        private MetricPointValueStorage deltaLastValue;

        internal MetricPoint(
            AggregatorStore aggregatorStore,
            AggregationType aggType,
            string[] keys,
            object[] values,
            double[] histogramExplicitBounds)
        {
            Debug.Assert(aggregatorStore != null, "AggregatorStore was null.");
            Debug.Assert((keys?.Length ?? 0) == (values?.Length ?? 0), "Key and value array lengths did not match.");
            Debug.Assert(histogramExplicitBounds != null, "Histogram explicit Bounds was null.");

            this.aggType = aggType;
            this.Tags = new ReadOnlyTagCollection(keys, values);
            this.runningValue = default;
            this.snapshotValue = default;
            this.deltaLastValue = default;
            this.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (this.aggType == AggregationType.HistogramWithBuckets ||
                this.aggType == AggregationType.HistogramWithMinMaxBuckets)
            {
                this.histogramBuckets = new HistogramBuckets(histogramExplicitBounds);
            }
            else if (this.aggType == AggregationType.Histogram ||
                     this.aggType == AggregationType.HistogramWithMinMax)
            {
                this.histogramBuckets = new HistogramBuckets(null);
            }
            else
            {
                this.histogramBuckets = null;
            }

            // Note: Intentionally set last because this is used to detect valid MetricPoints.
            this.aggregatorStore = aggregatorStore;
        }

        /// <summary>
        /// Gets the tags associated with the metric point.
        /// </summary>
        public readonly ReadOnlyTagCollection Tags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the start time associated with the metric point.
        /// </summary>
        public readonly DateTimeOffset StartTime => this.aggregatorStore.StartTimeExclusive;

        /// <summary>
        /// Gets the end time associated with the metric point.
        /// </summary>
        public readonly DateTimeOffset EndTime => this.aggregatorStore.EndTimeInclusive;

        internal MetricPointStatus MetricPointStatus
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        internal readonly bool IsInitialized => this.aggregatorStore != null;

        /// <summary>
        /// Gets the sum long value associated with the metric point.
        /// </summary>
        /// <remarks>
        /// Applies to <see cref="MetricType.LongSum"/> metric type.
        /// </remarks>
        /// <returns>Long sum value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly long GetSumLong()
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
        public readonly double GetSumDouble()
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
        public readonly long GetGaugeLastValueLong()
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
        public readonly double GetGaugeLastValueDouble()
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
        public readonly long GetHistogramCount()
        {
            if (this.aggType != AggregationType.HistogramWithBuckets &&
                this.aggType != AggregationType.Histogram &&
                this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
                this.aggType != AggregationType.HistogramWithMinMax)
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
        public readonly double GetHistogramSum()
        {
            if (this.aggType != AggregationType.HistogramWithBuckets &&
                this.aggType != AggregationType.Histogram &&
                this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
                this.aggType != AggregationType.HistogramWithMinMax)
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
        public readonly HistogramBuckets GetHistogramBuckets()
        {
            if (this.aggType != AggregationType.HistogramWithBuckets &&
                this.aggType != AggregationType.Histogram &&
                this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
                this.aggType != AggregationType.HistogramWithMinMax)
            {
                this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramBuckets));
            }

            return this.histogramBuckets;
        }

        /// <summary>
        /// Gets the Histogram Min and Max values.
        /// </summary>
        /// <param name="min"> The histogram minimum value.</param>
        /// <param name="max"> The histogram maximum value.</param>
        /// <returns>True if minimum and maximum value exist, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetHistogramMinMaxValues(out double min, out double max)
        {
            if (this.aggType == AggregationType.HistogramWithMinMax ||
                            this.aggType == AggregationType.HistogramWithMinMaxBuckets)
            {
                Debug.Assert(this.histogramBuckets != null, "histogramBuckets was null");

                min = this.histogramBuckets.SnapshotMin;
                max = this.histogramBuckets.SnapshotMax;
                return true;
            }

            min = 0;
            max = 0;
            return false;
        }

        internal readonly MetricPoint Copy()
        {
            MetricPoint copy = this;
            copy.histogramBuckets = this.histogramBuckets?.Copy();
            return copy;
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

                case AggregationType.HistogramWithBuckets:
                case AggregationType.HistogramWithMinMaxBuckets:
                case AggregationType.Histogram:
                case AggregationType.HistogramWithMinMax:
                    {
                        this.Update((double)number);

                        // At this point MetricPointStatus is already set to CollectPending so we can simply return.
                        return;
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
                        var sw = default(SpinWait);
                        while (true)
                        {
                            initValue = this.runningValue.AsDouble;

                            unchecked
                            {
                                newValue = initValue + number;
                            }

                            if (initValue == Interlocked.CompareExchange(ref this.runningValue.AsDouble, newValue, initValue))
                            {
                                break;
                            }

                            sw.SpinOnce();
                        }

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

                case AggregationType.HistogramWithBuckets:
                    {
                        int i = this.histogramBuckets.FindBucketIndex(number);

                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                unchecked
                                {
                                    this.runningValue.AsLong++;
                                    this.histogramBuckets.RunningSum += number;
                                    this.histogramBuckets.RunningBucketCounts[i]++;
                                }

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.Histogram:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                unchecked
                                {
                                    this.runningValue.AsLong++;
                                    this.histogramBuckets.RunningSum += number;
                                }

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.HistogramWithMinMaxBuckets:
                    {
                        int i = this.histogramBuckets.FindBucketIndex(number);

                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                unchecked
                                {
                                    this.runningValue.AsLong++;
                                    this.histogramBuckets.RunningSum += number;
                                    this.histogramBuckets.RunningBucketCounts[i]++;
                                    this.histogramBuckets.RunningMin = Math.Min(this.histogramBuckets.RunningMin, number);
                                    this.histogramBuckets.RunningMax = Math.Max(this.histogramBuckets.RunningMax, number);
                                }

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.HistogramWithMinMax:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                unchecked
                                {
                                    this.runningValue.AsLong++;
                                    this.histogramBuckets.RunningSum += number;
                                    this.histogramBuckets.RunningMin = Math.Min(this.histogramBuckets.RunningMin, number);
                                    this.histogramBuckets.RunningMax = Math.Max(this.histogramBuckets.RunningMax, number);
                                }

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
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

                case AggregationType.HistogramWithBuckets:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
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

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.Histogram:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                this.snapshotValue.AsLong = this.runningValue.AsLong;
                                this.histogramBuckets.SnapshotSum = this.histogramBuckets.RunningSum;

                                if (outputDelta)
                                {
                                    this.runningValue.AsLong = 0;
                                    this.histogramBuckets.RunningSum = 0;
                                }

                                this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.HistogramWithMinMaxBuckets:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                this.snapshotValue.AsLong = this.runningValue.AsLong;
                                this.histogramBuckets.SnapshotSum = this.histogramBuckets.RunningSum;
                                this.histogramBuckets.SnapshotMin = this.histogramBuckets.RunningMin;
                                this.histogramBuckets.SnapshotMax = this.histogramBuckets.RunningMax;

                                if (outputDelta)
                                {
                                    this.runningValue.AsLong = 0;
                                    this.histogramBuckets.RunningSum = 0;
                                    this.histogramBuckets.RunningMin = double.PositiveInfinity;
                                    this.histogramBuckets.RunningMax = double.NegativeInfinity;
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

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }

                case AggregationType.HistogramWithMinMax:
                    {
                        var sw = default(SpinWait);
                        while (true)
                        {
                            if (Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 1) == 0)
                            {
                                // Lock acquired
                                this.snapshotValue.AsLong = this.runningValue.AsLong;
                                this.histogramBuckets.SnapshotSum = this.histogramBuckets.RunningSum;
                                this.histogramBuckets.SnapshotMin = this.histogramBuckets.RunningMin;
                                this.histogramBuckets.SnapshotMax = this.histogramBuckets.RunningMax;

                                if (outputDelta)
                                {
                                    this.runningValue.AsLong = 0;
                                    this.histogramBuckets.RunningSum = 0;
                                    this.histogramBuckets.RunningMin = double.PositiveInfinity;
                                    this.histogramBuckets.RunningMax = double.NegativeInfinity;
                                }

                                this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                                // Release lock
                                Interlocked.Exchange(ref this.histogramBuckets.IsCriticalSectionOccupied, 0);
                                break;
                            }

                            sw.SpinOnce();
                        }

                        break;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private readonly void ThrowNotSupportedMetricTypeException(string methodName)
        {
            throw new NotSupportedException($"{methodName} is not supported for this metric type.");
        }
    }
}
