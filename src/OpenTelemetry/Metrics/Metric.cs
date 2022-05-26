// <copyright file="Metric.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Represents a Metric stream which can contain multiple MetricPoints.
    /// </summary>
    public sealed class Metric
    {
        internal static readonly double[] DefaultHistogramBounds = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000 };

        private readonly AggregatorStore aggStore;

        internal Metric(
            MetricStreamIdentity instrumentIdentity,
            AggregationTemporality temporality,
            int maxMetricPointsPerMetricStream,
            double[] histogramBounds = null,
            string[] tagKeysInteresting = null)
        {
            this.InstrumentIdentity = instrumentIdentity;

            AggregationType aggType;
            if (instrumentIdentity.InstrumentType == typeof(ObservableCounter<long>)
                || instrumentIdentity.InstrumentType == typeof(ObservableCounter<int>)
                || instrumentIdentity.InstrumentType == typeof(ObservableCounter<short>)
                || instrumentIdentity.InstrumentType == typeof(ObservableCounter<byte>))
            {
                aggType = AggregationType.LongSumIncomingCumulative;
                this.MetricType = MetricType.LongSum;
            }
            else if (instrumentIdentity.InstrumentType == typeof(Counter<long>)
                || instrumentIdentity.InstrumentType == typeof(Counter<int>)
                || instrumentIdentity.InstrumentType == typeof(Counter<short>)
                || instrumentIdentity.InstrumentType == typeof(Counter<byte>))
            {
                aggType = AggregationType.LongSumIncomingDelta;
                this.MetricType = MetricType.LongSum;
            }
            else if (instrumentIdentity.InstrumentType == typeof(Counter<double>)
                || instrumentIdentity.InstrumentType == typeof(Counter<float>))
            {
                aggType = AggregationType.DoubleSumIncomingDelta;
                this.MetricType = MetricType.DoubleSum;
            }
            else if (instrumentIdentity.InstrumentType == typeof(ObservableCounter<double>)
                || instrumentIdentity.InstrumentType == typeof(ObservableCounter<float>))
            {
                aggType = AggregationType.DoubleSumIncomingCumulative;
                this.MetricType = MetricType.DoubleSum;
            }
            else if (instrumentIdentity.InstrumentType == typeof(ObservableGauge<double>)
                || instrumentIdentity.InstrumentType == typeof(ObservableGauge<float>))
            {
                aggType = AggregationType.DoubleGauge;
                this.MetricType = MetricType.DoubleGauge;
            }
            else if (instrumentIdentity.InstrumentType == typeof(ObservableGauge<long>)
                || instrumentIdentity.InstrumentType == typeof(ObservableGauge<int>)
                || instrumentIdentity.InstrumentType == typeof(ObservableGauge<short>)
                || instrumentIdentity.InstrumentType == typeof(ObservableGauge<byte>))
            {
                aggType = AggregationType.LongGauge;
                this.MetricType = MetricType.LongGauge;
            }
            else if (instrumentIdentity.InstrumentType == typeof(Histogram<long>)
                || instrumentIdentity.InstrumentType == typeof(Histogram<int>)
                || instrumentIdentity.InstrumentType == typeof(Histogram<short>)
                || instrumentIdentity.InstrumentType == typeof(Histogram<byte>)
                || instrumentIdentity.InstrumentType == typeof(Histogram<float>)
                || instrumentIdentity.InstrumentType == typeof(Histogram<double>))
            {
                this.MetricType = MetricType.Histogram;

                if (histogramBounds != null
                    && histogramBounds.Length == 0)
                {
                    aggType = AggregationType.HistogramSumCount;
                }
                else
                {
                    aggType = AggregationType.Histogram;
                }
            }
            else
            {
                throw new NotSupportedException($"Unsupported Instrument Type: {instrumentIdentity.InstrumentType.FullName}");
            }

            this.aggStore = new AggregatorStore(instrumentIdentity.InstrumentName, aggType, temporality, maxMetricPointsPerMetricStream, histogramBounds ?? DefaultHistogramBounds, tagKeysInteresting);
            this.Temporality = temporality;
            this.InstrumentDisposed = false;
        }

        public MetricType MetricType { get; private set; }

        public AggregationTemporality Temporality { get; private set; }

        public string Name => this.InstrumentIdentity.InstrumentName;

        public string Description => this.InstrumentIdentity.Description;

        public string Unit => this.InstrumentIdentity.Unit;

        public string MeterName => this.InstrumentIdentity.MeterName;

        public string MeterVersion => this.InstrumentIdentity.MeterVersion;

        internal MetricStreamIdentity InstrumentIdentity { get; private set; }

        internal bool InstrumentDisposed { get; set; }

        public MetricPointsAccessor GetMetricPoints()
        {
            return this.aggStore.GetMetricPoints();
        }

        internal void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.aggStore.Update(value, tags);
        }

        internal void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.aggStore.Update(value, tags);
        }

        internal int Snapshot()
        {
            return this.aggStore.Snapshot();
        }
    }
}
