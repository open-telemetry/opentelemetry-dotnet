﻿// <copyright file="MetricProcessor.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics.Export
{
    public abstract class MetricProcessor
    {
        /// <summary>
        /// Process the counter metric.
        /// </summary>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="sumAggregator">the aggregator from which sum can be obtained.</param>
        public abstract void ProcessCounter(string metricName, LabelSet labelSet, SumAggregator<long> sumAggregator);

        /// <summary>
        /// Process the counter metric.
        /// </summary>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="sumAggregator">the aggregator from which sum can be obtained.</param>
        public abstract void ProcessCounter(string metricName, LabelSet labelSet, SumAggregator<double> sumAggregator);

        /// <summary>
        /// Process the gauge metric.
        /// </summary>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="gaugeAggregator">the aggregator from which current value can be obtained.</param>
        public abstract void ProcessGauge(string metricName, LabelSet labelSet, GaugeAggregator<long> gaugeAggregator);

        /// <summary>
        /// Process the gauge metric.
        /// </summary>
        /// <param name="metricName">the name of the counter.</param>
        /// <param name="labelSet">the labelSet associated with counter value.</param>
        /// <param name="gaugeAggregator">the aggregator from which current value can be obtained.</param>
        public abstract void ProcessGauge(string metricName, LabelSet labelSet, GaugeAggregator<double> gaugeAggregator);
    }
}
