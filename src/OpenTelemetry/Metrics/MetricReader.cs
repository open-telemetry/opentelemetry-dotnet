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
        public BaseProvider ParentProvider { get; private set; }

        public virtual void Collect()
        {
            var collectMetric = this.ParentProvider.GetMetricCollect();
            var metricsCollected = collectMetric();
            this.OnCollect(metricsCollected);
        }

        public virtual void OnCollect(Batch<Metric> metrics)
        {
        }

        public virtual AggregationTemporality GetAggregationTemporality()
        {
            return AggregationTemporality.Cumulative;
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
    }
}
