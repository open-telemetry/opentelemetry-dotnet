// <copyright file="PrometheusExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public class PrometheusExporterTests
    {
        [Fact]
        public void PrometheusExporterTest1()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            Metric<long> metric = new Metric<long>("sample");
            var promExporter = new PrometheusExporter<long>(promOptions, metric);
            try
            {
                promExporter.Start();
                List<KeyValuePair<string, string>> label1 = new List<KeyValuePair<string, string>>();
                label1.Add(new KeyValuePair<string, string>("dim1", "value1"));
                var labelSet1 = new LabelSet(label1);
                metric.GetOrCreateMetricTimeSeries(labelSet1).Add(100);
                metric.GetOrCreateMetricTimeSeries(labelSet1).Add(200);
            }
            finally
            {
                Task.Delay(10000).Wait();
                promExporter.Stop();
            }
        }
    }
}
