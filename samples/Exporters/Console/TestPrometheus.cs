// <copyright file="TestPrometheus.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using System.Diagnostics;
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
        internal static async Task<object> RunAsync(int port, int pushIntervalInSecs, int totalDurationInMins)
        {
            Console.WriteLine($"OpenTelemetry Prometheus Exporter is making metrics available at http://localhost:{port}/metrics/");

            /*
            Following is sample prometheus.yml config. Adjust port,interval as needed.

            scrape_configs:
              # The job name is added as a label `job=<job_name>` to any timeseries scraped from this config.
              - job_name: 'OpenTelemetryTest'

                # metrics_path defaults to '/metrics'
                # scheme defaults to 'http'.

                static_configs:
                - targets: ['localhost:9184']
            */

            // Create and Setup Prometheus Exporter
            var promOptions = new PrometheusExporterOptions() { Url = $"http://localhost:{port}/metrics/" };
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
                mb.SetMetricPushInterval(TimeSpan.FromSeconds(pushIntervalInSecs));
                });

            // Obtain a Meter. Libraries would pass their name as argument.
            var meter = meterFactory.GetMeter("MyMeter");

            // the rest is purely from Metric API.
            var testCounter = meter.CreateInt64Counter("MyCounter");
            var testMeasure = meter.CreateInt64Measure("MyMeasure");
            var testObserver = meter.CreateInt64Observer("MyObservation", CallBackForMyObservation);
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));
            var defaultContext = default(SpanContext);

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalMinutes < totalDurationInMins)
            {
                testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));

                testMeasure.Record(defaultContext, 100, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 500, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 5, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 750, meter.GetLabelSet(labels1));

                // Obviously there is no testObserver.Oberve() here, as Observer instruments
                // have callbacks that are called by the Meter automatically at each collection interval.

                await Task.Delay(1000);
                var remaining = (totalDurationInMins * 60) - sw.Elapsed.TotalSeconds;
                Console.WriteLine("Running and emitting metrics. Remaining time:" + (int)remaining + " seconds");
            }

            // Stopping 
            metricsHttpServer.Stop();
            Console.WriteLine("Metrics server shutdown.");
            Console.WriteLine("Press Enter key to exit.");
            return null;
        }

        internal static void CallBackForMyObservation(Int64ObserverMetric observerMetric)
        {
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            observerMetric.Observe(Process.GetCurrentProcess().WorkingSet64, labels1);
        }
    }
}
