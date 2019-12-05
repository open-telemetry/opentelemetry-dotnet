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
    public class Metric<T>
    {
        public Metric(string name)
        {
            this.MetricName = name;
            this.MetricDescription = "Description:" + name;
        }

        public string MetricName { get; private set; }

        public string MetricDescription { get; private set; } 

        public IDictionary<LabelSet, MetricTimeSeries<T>> TimeSeries { get; } = new ConcurrentDictionary<LabelSet, MetricTimeSeries<T>>();

        public MetricTimeSeries<T> GetOrCreateMetricTimeSeries(LabelSet labelSet)
        {
            if (this.TimeSeries.ContainsKey(labelSet))
            {
                return this.TimeSeries[labelSet];
            }
            else
            {
                return this.CreateMetricTimeSeries(labelSet);
            }
        }

        private MetricTimeSeries<T> CreateMetricTimeSeries(LabelSet labelSet)
        {
            var newSeries = new MetricTimeSeries<T>(labelSet);
            this.TimeSeries.Add(labelSet, newSeries);
            return newSeries;
        }
    }
}
