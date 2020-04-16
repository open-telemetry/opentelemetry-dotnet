// <copyright file="MetricProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Aggregators;

namespace OpenTelemetry.Metrics.Export
{
    public abstract class MetricProcessor
    {
        /// <summary>
        /// Process the metric. This method is called once every collection interval.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the instrument.</param>
        /// <param name="labelSet">the labelSet associated with the instrument.</param>
        /// <param name="aggregator">the aggregator used.</param>
        public abstract void Process(string meterName, string metricName, LabelSet labelSet, Aggregator<long> aggregator);

        /// <summary>
        /// Process the metric. This method is called once every collection interval.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the instrument.</param>
        /// <param name="labelSet">the labelSet associated with the instrument.</param>
        /// <param name="aggregator">the aggregator used.</param>
        public abstract void Process(string meterName, string metricName, LabelSet labelSet, Aggregator<double> aggregator);

        /// <summary>
        /// Finish the current collection cycle and return the metrics it holds.
        /// This is called at the end of one collection cycle by the Controller.
        /// MetricProcessor can use this to clear its Metrics (in case of stateless).
        /// </summary>
        /// <param name="longMetrics">The list of long metrics from this cycle, which are to be exported.</param>
        /// <param name="doubleMetrics">The list of double metrics from this cycle, which are to be exported.</param>
        public abstract void FinishCollectionCycle(out IEnumerable<Metric<long>> longMetrics, out IEnumerable<Metric<double>> doubleMetrics);
    }
}
