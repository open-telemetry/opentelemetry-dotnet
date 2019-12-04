// <copyright file="TestMetricProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Export
{
    internal class TestMetricProcessor : MetricProcessor
    {
        public List<Tuple<string, LabelSet, long>> counters = new List<Tuple<string, LabelSet, long>>();

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, SumAggregator<long> sumAggregator)
        {
            counters.Add(new Tuple<string, LabelSet, long>(metricName, labelSet, sumAggregator.Sum()));
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, SumAggregator<double> sumAggregator)
        {
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<long> gaugeAggregator)
        {
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<double> gaugeAggregator)
        {
        }
    }
}
