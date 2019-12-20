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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics.Implementation
{
    /// <summary>
    /// This class need to evolve to what's referred to as Export Record in the spec.
    /// An exporter-independent in-memory representation combining the metric instrument, the LabelSet for export, and the associated aggregate state.
    /// </summary>
    public class Metric
    {
        public Metric(string metricNamespace,
            string metricName,
            string desc,
            InstrumentKind kind,
            IEnumerable<KeyValuePair<string, string>> labels,
            MetricValue<long> value)
        {
            this.MetricNamespace = metricNamespace;
            this.MetricName = metricName;
            this.MetricDescription = desc;
            this.Kind = kind;
            this.Labels = labels;
            this.LongValue = value;
        }

        public Metric(string metricNamespace,
            string metricName,
            string desc,
            InstrumentKind kind,
            IEnumerable<KeyValuePair<string, string>> labels,
            MetricValue<double> value)
        {
            this.MetricNamespace = metricNamespace;
            this.MetricName = metricName;
            this.MetricDescription = desc;
            this.Kind = kind;
            this.Labels = labels;
            this.DoubleValue = value;
        }

        public string MetricNamespace { get; private set; }

        public string MetricName { get; private set; }

        public string MetricDescription { get; private set; }

        public InstrumentKind Kind { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> Labels { get; set; }

        public MetricValue<long> LongValue { get; set; }

        public MetricValue<double> DoubleValue { get; set; }
    }
}
