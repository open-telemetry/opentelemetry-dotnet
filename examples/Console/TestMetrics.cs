// <copyright file="TestMetrics.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Examples.Console
{
    internal class TestMetrics
    {
        internal static object Run(MetricsOptions options)
        {
            var providerBuilder = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("myservice"))
                .AddSource("TestMeter"); // All instruments from this meter are enabled.

            if (options.UseExporter.ToLower() == "otlp")
            {
                /*
                 * Prerequisite to run this example:
                 * Set up an OpenTelemetry Collector to run on local docker.
                 *
                 * Open a terminal window at the examples/Console/ directory and
                 * launch the OpenTelemetry Collector with an OTLP receiver, by running:
                 *
                 *  - On Unix based systems use:
                 *     docker run --rm -it -p 4317:4317 -v $(pwd):/cfg otel/opentelemetry-collector:0.33.0 --config=/cfg/otlp-collector-example/config.yaml
                 *
                 *  - On Windows use:
                 *     docker run --rm -it -p 4317:4317 -v "%cd%":/cfg otel/opentelemetry-collector:0.33.0 --config=/cfg/otlp-collector-example/config.yaml
                 *
                 * Open another terminal window at the examples/Console/ directory and
                 * launch the OTLP example by running:
                 *
                 *     dotnet run metrics --useExporter otlp
                 *
                 * The OpenTelemetry Collector will output all received metrics to the stdout of its terminal.
                 *
                 */

                // Adding the OtlpExporter creates a GrpcChannel.
                // This switch must be set before creating a GrpcChannel/HttpClient when calling an insecure gRPC service.
                // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                providerBuilder
                    .AddOtlpExporter(o =>
                    {
                        o.MetricExportIntervalMilliseconds = options.DefaultCollectionPeriodMilliseconds;
                        o.AggregationTemporality = options.IsDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative;
                    });
            }
            else
            {
                providerBuilder
                    .AddConsoleExporter(o =>
                    {
                        o.MetricExportIntervalMilliseconds = options.DefaultCollectionPeriodMilliseconds;
                        o.AggregationTemporality = options.IsDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative;
                    });
            }

            using var provider = providerBuilder.Build();

            using var meter = new Meter("TestMeter", "0.0.1");

            Counter<int> counter = null;
            if (options.FlagCounter ?? true)
            {
                counter = meter.CreateCounter<int>("counter", "things", "A count of things");
            }

            Histogram<int> histogram = null;
            if (options.FlagHistogram ?? false)
            {
                histogram = meter.CreateHistogram<int>("histogram");
            }

            if (options.FlagGauge ?? false)
            {
                var observableCounter = meter.CreateObservableGauge<int>("gauge", () =>
                {
                    return new List<Measurement<int>>()
                    {
                        new Measurement<int>(
                            (int)Process.GetCurrentProcess().PrivateMemorySize64,
                            new KeyValuePair<string, object>("tag1", "value1")),
                    };
                });
            }

            var cts = new CancellationTokenSource();

            var tasks = new List<Task>();

            for (int i = 0; i < options.NumTasks; i++)
            {
                var taskno = i;

                tasks.Add(Task.Run(() =>
                {
                    System.Console.WriteLine($"Task started {taskno + 1}/{options.NumTasks}.");

                    var loops = 0;

                    while (!cts.IsCancellationRequested)
                    {
                        if (options.MaxLoops > 0 && loops >= options.MaxLoops)
                        {
                            break;
                        }

                        histogram?.Record(10);

                        histogram?.Record(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        histogram?.Record(
                            200,
                            new KeyValuePair<string, object>("tag1", "value2"),
                            new KeyValuePair<string, object>("tag2", "value2"));

                        histogram?.Record(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        histogram?.Record(
                            200,
                            new KeyValuePair<string, object>("tag2", "value2"),
                            new KeyValuePair<string, object>("tag1", "value2"));

                        counter?.Add(10);

                        counter?.Add(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        counter?.Add(
                            200,
                            new KeyValuePair<string, object>("tag1", "value2"),
                            new KeyValuePair<string, object>("tag2", "value2"));

                        counter?.Add(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        counter?.Add(
                            200,
                            new KeyValuePair<string, object>("tag2", "value2"),
                            new KeyValuePair<string, object>("tag1", "value2"));

                        loops++;
                    }
                }));
            }

            cts.CancelAfter(options.RunTime);
            System.Console.WriteLine($"Wait for {options.RunTime} milliseconds.");
            while (!cts.IsCancellationRequested)
            {
                Task.Delay(1000).Wait();
            }

            Task.WaitAll(tasks.ToArray());

            return null;
        }
    }
}
