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

using System;
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
        private static readonly Meter MyMeter = new Meter("TestMeter");
        private static readonly Counter<double> Counter = MyMeter.CreateCounter<double>("myCounter", description: "A counter for demonstration purpose.");
        private static readonly Histogram<long> MyHistogram = MyMeter.CreateHistogram<long>("myHistogram");
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random());

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
                .AddMeter(MyMeter.Name)
                .AddPrometheusExporter(opt =>
                {
                    opt.StartHttpListener = true;
                    opt.HttpListenerPrefixes = new string[] { $"http://localhost:{port}/" };
                })
                .Build();

            ObservableGauge<long> gauge = MyMeter.CreateObservableGauge(
            "myGauge",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(ThreadLocalRandom.Value.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2")),
                    new Measurement<long>(ThreadLocalRandom.Value.Next(1, 1000), new("tag1", "value1"), new("tag2", "value3")),
                };
            },
            description: "A gauge for demonstration purpose.");

            using var token = new CancellationTokenSource();
            Task writeMetricTask = new Task(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Counter.Add(9.9, new("name", "apple"), new("color", "red"));
                    Counter.Add(99.9, new("name", "lemon"), new("color", "yellow"));
                    MyHistogram.Record(ThreadLocalRandom.Value.Next(1, 1500), new("tag1", "value1"), new("tag2", "value2"));

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
