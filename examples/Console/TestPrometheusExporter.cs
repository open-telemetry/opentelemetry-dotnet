// <copyright file="TestPrometheusExporter.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestPrometheusExporter
    {
        private static readonly Meter MyMeter = new Meter("TestMeter", "0.0.1");
        private static readonly Counter<long> Counter = MyMeter.CreateCounter<long>("counter");

        internal static object Run(int port, int totalDurationInMins)
        {
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
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddPrometheusExporter(opt => opt.Url = $"http://localhost:{port}/metrics/")
                .Build();

            using var token = new CancellationTokenSource();
            Task writeMetricTask = new Task(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Counter.Add(
                                10,
                                new KeyValuePair<string, object>("tag1", "value1"),
                                new KeyValuePair<string, object>("tag2", "value2"));

                    Counter.Add(
                                100,
                                new KeyValuePair<string, object>("tag1", "anothervalue"),
                                new KeyValuePair<string, object>("tag2", "somethingelse"));
                    Task.Delay(10).Wait();
                }
            });
            writeMetricTask.Start();

            token.CancelAfter(totalDurationInMins * 60 * 1000);

            System.Console.WriteLine($"OpenTelemetry Prometheus Exporter is making metrics available at http://localhost:{port}/metrics/");
            System.Console.WriteLine($"Press Enter key to exit now or will exit automatically after {totalDurationInMins} minutes.");
            System.Console.ReadLine();
            token.Cancel();
            System.Console.WriteLine("Exiting...");
            return null;
        }
    }
}
