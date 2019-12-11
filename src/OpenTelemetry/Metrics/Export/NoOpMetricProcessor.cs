// <copyright file="NoOpMetricProcessor.cs" company="OpenTelemetry Authors">
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
    internal class NoOpMetricProcessor : MetricProcessor
    {
        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<long> sumAggregator)
        {
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<double> sumAggregator)
        {
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<long> gaugeAggregator)
        {
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<double> gaugeAggregator)
        {
        }

        public override void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<long> measureAggregator)
        {
        }

        public override void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<double> measureAggregator)
        {
        }
    }
}
