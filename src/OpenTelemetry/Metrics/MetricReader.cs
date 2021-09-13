// <copyright file="MetricReader.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    public abstract class MetricReader : IDisposable
    {
        private AggregationTemporality preferredAggregationTemporality = AggregationTemporality.Both;
        private AggregationTemporality supportedAggregationTemporality = AggregationTemporality.Both;

        public BaseProvider ParentProvider { get; private set; }

        public AggregationTemporality PreferredAggregationTemporality
        {
            get => this.preferredAggregationTemporality;
            set
            {
                ValidateAggregationTemporality(value, this.supportedAggregationTemporality);
                this.preferredAggregationTemporality = value;
            }
        }

        public AggregationTemporality SupportedAggregationTemporality
        {
            get => this.supportedAggregationTemporality;
            set
            {
                ValidateAggregationTemporality(this.preferredAggregationTemporality, value);
                this.supportedAggregationTemporality = value;
            }
        }

        public virtual void Collect()
        {
            var collectMetric = this.ParentProvider.GetMetricCollect();
            var metricsCollected = collectMetric();
            this.OnCollect(metricsCollected);
        }

        public virtual void OnCollect(Batch<Metric> metrics)
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal virtual void SetParentProvider(BaseProvider parentProvider)
        {
            this.ParentProvider = parentProvider;
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        private static void ValidateAggregationTemporality(AggregationTemporality preferred, AggregationTemporality supported)
        {
            if ((int)(preferred & AggregationTemporality.Both) == 0)
            {
                throw new ArgumentException($"PreferredAggregationTemporality has an invalid value {preferred}.", nameof(preferred));
            }

            if ((int)(supported & AggregationTemporality.Both) == 0)
            {
                throw new ArgumentException($"SupportedAggregationTemporality has an invalid value {supported}.", nameof(supported));
            }

            /*
            | Preferred  | Supported  | Valid |
            | ---------- | ---------- | ----- |
            | Both       | Both       | true  |
            | Both       | Cumulative | false |
            | Both       | Delta      | false |
            | Cumulative | Both       | true  |
            | Cumulative | Cumulative | true  |
            | Cumulative | Delta      | false |
            | Delta      | Both       | true  |
            | Delta      | Cumulative | false |
            | Delta      | Delta      | true  |
            */
            if ((int)(preferred & supported) == 0 || preferred > supported)
            {
                throw new ArgumentException($"PreferredAggregationTemporality {preferred} and SupportedAggregationTemporality {supported} are incompatible.");
            }
        }
    }
}
