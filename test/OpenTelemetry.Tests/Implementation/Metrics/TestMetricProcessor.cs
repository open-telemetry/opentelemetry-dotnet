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
using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Test
{
    internal class TestMetricProcessor : MetricProcessor
    {
        public List<Metric<long>> longMetrics = new List<Metric<long>>();
        public List<Metric<double>> doubleMetrics = new List<Metric<double>>();

        public override void FinishCollectionCycle(out IEnumerable<Metric<long>> longMetrics, out IEnumerable<Metric<double>> doubleMetrics)
        {
            longMetrics = this.longMetrics;
            doubleMetrics = this.doubleMetrics;
            this.longMetrics = new List<Metric<long>>();
            this.doubleMetrics = new List<Metric<double>>();
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
