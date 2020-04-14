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

using System;
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
        /// Constructs UngroupedBatcher.
        /// </summary>
        public UngroupedBatcher()
        {                        
            this.longMetrics = new List<Metric<long>>();
            this.doubleMetrics = new List<Metric<double>>();
        }

        public override void Process(string meterName, string metricName, LabelSet labelSet, Aggregator<long> aggregator)
        {
            var metric = new Metric<long>(meterName, metricName, meterName + metricName, labelSet.Labels, aggregator.GetAggregationType());
            metric.Data = aggregator.ToMetricData();
            this.longMetrics.Add(metric);
        }

        public override void Process(string meterName, string metricName, LabelSet labelSet, Aggregator<double> aggregator)
        {
            var metric = new Metric<double>(meterName, metricName, meterName + metricName, labelSet.Labels, aggregator.GetAggregationType());
            metric.Data = aggregator.ToMetricData();
            this.doubleMetrics.Add(metric);
        }

        public override Tuple<IEnumerable<Metric<long>>, IEnumerable<Metric<double>>> FinishCollectionCycle()
        {
            // The batcher is currently stateless. i.e it forgets state after collection is done.
            // Once the spec is ready for stateless vs stateful, we need to modify batcher
            // to remember or clear state after each cycle.
            List<Metric<long>> longMetricToExport = null;
            List<Metric<double>> doubleMetricToExport = null;

            if (this.longMetrics.Count > 0)
            {
                longMetricToExport = this.longMetrics;
                this.longMetrics = new List<Metric<long>>();                
            }

            if (this.doubleMetrics.Count > 0)
            {
                doubleMetricToExport = this.doubleMetrics;
                this.doubleMetrics = new List<Metric<double>>();                
            }

            OpenTelemetrySdkEventSource.Log.BatcherCollectionCompleted(longMetricToExport.Count + doubleMetricToExport.Count);
            return new Tuple<IEnumerable<Metric<long>>, IEnumerable<Metric<double>>>(longMetricToExport, doubleMetricToExport);
        }
    }
}
