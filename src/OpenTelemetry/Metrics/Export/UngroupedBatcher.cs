// <copyright file="UngroupedBatcher.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics.Aggregators;

namespace OpenTelemetry.Metrics.Export
{
    /// <summary>
    /// Batcher which retains all dimensions/labels.
    /// </summary>
    public class UngroupedBatcher : MetricProcessor
    {
        private List<Metric<long>> longMetrics;
        private List<Metric<double>> doubleMetrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="UngroupedBatcher"/> class.
        /// </summary>
        public UngroupedBatcher()
        {
            this.longMetrics = new List<Metric<long>>();
            this.doubleMetrics = new List<Metric<double>>();
        }

        public override void FinishCollectionCycle(out IEnumerable<Metric<long>> longMetrics, out IEnumerable<Metric<double>> doubleMetrics)
        {
            // The batcher is currently stateless. i.e it forgets state after collection is done.
            // Once the spec is ready for stateless vs stateful, we need to modify batcher
            // to remember or clear state after each cycle.
            longMetrics = this.longMetrics;
            doubleMetrics = this.doubleMetrics;

            var count = this.longMetrics.Count + this.doubleMetrics.Count;
            this.longMetrics = new List<Metric<long>>();
            this.doubleMetrics = new List<Metric<double>>();

            OpenTelemetrySdkEventSource.Log.BatcherCollectionCompleted(count);
        }

        public override void Process(Metric<long> metric)
        {
            this.longMetrics.Add(metric);
        }

        public override void Process(Metric<double> metric)
        {
            this.doubleMetrics.Add(metric);
        }
    }
}
