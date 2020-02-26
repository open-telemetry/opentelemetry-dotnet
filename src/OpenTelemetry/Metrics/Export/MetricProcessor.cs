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

using OpenTelemetry.Metrics.Aggregators;

namespace OpenTelemetry.Metrics.Export
{
    public abstract class MetricProcessor
    {
        /// <summary>
        /// Process the counter metric.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="sumAggregator">the aggregator from which sum can be obtained.</param>
        public abstract void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<long> sumAggregator);

        /// <summary>
        /// Process the counter metric.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="sumAggregator">the aggregator from which sum can be obtained.</param>
        public abstract void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<double> sumAggregator);

        /// <summary>
        /// Process the measure metric.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="measureAggregator">the aggregator from which raw values can be obtained.</param>
        public abstract void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<long> measureAggregator);

        /// <summary>
        /// Process the measure metric.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="measureAggregator">the aggregator from which raw values can be obtained.</param>
        public abstract void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<double> measureAggregator);

        /// <summary>
        /// Process the observer metric.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the observer.</param>
        /// <param name="labelSet">the labelSet associated with observer value.</param>
        /// <param name="lastValueAggregator">the aggregator from which raw values can be obtained.</param>
        public abstract void ProcessObserver(string meterName, string metricName, LabelSet labelSet, LastValueAggregator<long> lastValueAggregator);

        /// <summary>
        /// Process the observer metric.
        /// </summary>
        /// <param name="meterName">the name of the meter, used as a namespace for the metric instruments.</param>
        /// <param name="metricName">the name of the observer.</param>
        /// <param name="labelSet">the labelSet associated with observer value.</param>
        /// <param name="lastValueAggregator">the aggregator from which raw values can be obtained.</param>
        public abstract void ProcessObserver(string meterName, string metricName, LabelSet labelSet, LastValueAggregator<double> lastValueAggregator);
    }
}
