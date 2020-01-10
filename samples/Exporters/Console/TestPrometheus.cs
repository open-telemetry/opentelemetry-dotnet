// <copyright file="TestPrometheus.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;

namespace Samples
{
    internal class TestPrometheus
    {
        internal static object Run()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            var promExporter = new PrometheusExporter(promOptions);
            var simpleProcessor = new UngroupedBatcher(promExporter, TimeSpan.FromSeconds(5));
            var meter = MeterFactory.Create(simpleProcessor).GetMeter("library1");
            var testCounter = meter.CreateInt64Counter("testCounter");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var defaultContext = default(SpanContext);
            try
            {
                promExporter.Start();

                for (int i = 0; i < 1000; i++)
                {
                    testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));
                    testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels1));
                    testCounter.Add(defaultContext, 200, meter.GetLabelSet(labels2));
                    testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels2));

                    if (i % 10 == 0)
                    {
                        // Collect is called here explicitly as there is 
                        // no controller implementation yet.
                        // TODO: There should be no need to cast to MeterSdk.
                        (meter as MeterSdk).Collect();
                    }

                    Task.Delay(1000).Wait();
                }
            }
            finally
            {
                Task.Delay(3000).Wait();
                promExporter.Stop();
            }

            return null;
        }
    }
}
