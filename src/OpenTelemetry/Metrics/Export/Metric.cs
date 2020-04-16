// <copyright file="Metric.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Export
{
    /// <summary>
    /// This class would evolve to become the export record.
    /// </summary>
    /// <typeparam name="T">Type of the metric - long or double currently.</typeparam>
    public class Metric<T>
    {
        public Metric(
            string metricNamespace,
            string metricName,
            string desc,
            IEnumerable<KeyValuePair<string, string>> labels,
            AggregationType type)
        {
            this.MetricNamespace = metricNamespace;
            this.MetricName = metricName;
            this.MetricDescription = desc;
            this.Labels = labels;
            this.AggregationType = type;
        }

        public string MetricNamespace { get; private set; }

        public string MetricName { get; private set; }

        public string MetricDescription { get; private set; }

        public AggregationType AggregationType { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> Labels { get; private set; }

        public MetricData<T> Data { get; internal set; }
    }
}
