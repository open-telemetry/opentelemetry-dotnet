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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;

namespace Samples
{
    internal class TestPrometheus
    {
        internal static object Run()
        {
            // Create and Setup Prometheus Exporter
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            var promExporter = new PrometheusExporter(promOptions);
            var metricsHttpServer = new PrometheusExporterMetricsHttpServer(promExporter);
            metricsHttpServer.Start();

            // Creater Processor (called Batcher in Metric spec, this is still not decided)
            var processor = new UngroupedBatcher();

            // MeterFactory is from where one can obtain Meters.
            // All meters from this factory will be configured with the common processor.
            var meterFactory = MeterFactory.Create(mb =>
                {
                mb.SetMetricProcessor(processor);
                mb.SetMetricExporter(promExporter);
                mb.SetMetricPushInterval(TimeSpan.FromSeconds(30));
                });

            // Obtain a Meter. Libraries would pass their name as argument.
            var meter = meterFactory.GetMeter("MyMeter");

            // the rest is purely from Metric API.
            var testCounter = meter.CreateInt64Counter("MyCounter");
            var testMeasure = meter.CreateInt64Measure("MyMeasure");
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));
            var defaultContext = default(SpanContext);

            // TODO: This sample runs indefinitely. Replace with actual shutdown logic.
            while (true)
            {
                testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 100, meter.GetLabelSet(labels1));

                Task.Delay(100).Wait();
            }

            // Stopping 
            // metricsHttpServer.Stop();
        }
    }
}
