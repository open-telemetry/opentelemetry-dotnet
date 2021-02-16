// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;

#pragma warning disable CS0618

namespace HttpServerExample
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            CancellationTokenSource cancelSrc = new CancellationTokenSource();

            var client1Task = Client.StartClientTask("http://127.0.0.1:3000/Test1", 1000, cancelSrc.Token);
            var client2Task = Client.StartClientTask("http://127.0.0.1:3000/Test2", 5000, cancelSrc.Token);

            // My Application

            var pipeFiveSeconds = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
                .SetPushInterval(TimeSpan.FromSeconds(5))
                .Build();

            var pipeOneMinute = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
                .SetPushInterval(TimeSpan.FromSeconds(1))

                // No way to specify which metrics I'm interested in!
                .SetProcessor(new MyFilterProcessor("ServerRoomTemp"))

                .Build();

            // How do I configure the Default provider?
            // How do I tie Library to multiple pipeline?
            MeterProvider.SetDefault(pipeOneMinute);

            // ========

            var webserver = new WebServer();
            var webserverTask = webserver.StartServerTask("http://127.0.0.1:3000/", cancelSrc.Token);

            Console.WriteLine("Press [ENTER] to exit.");
            var cmdline = Console.ReadLine();
            cancelSrc.Cancel();

            webserver.Shutdown();
            await webserverTask;

            // ========

            // There is no Shutdown()
            // pipeFiveSeconds.Shutdown();
            // pipeOneMinute.Shutdown();
        }

        public class MyFilterProcessor : MetricProcessor
        {
            private static readonly object lockList = new object();
            private static List<Metric> list = new List<Metric>();

            private string[] filters;

            public MyFilterProcessor(params string[] filters)
            {
                this.filters = filters;
            }

            public override void FinishCollectionCycle(out IEnumerable<Metric> metrics)
            {
                var oldList = Interlocked.Exchange(ref list, new List<Metric>());
                metrics = oldList;
            }

            public override void Process(Metric metric)
            {
                Console.WriteLine($"Processing {metric.MetricName}, count={metric.Data.Count}...");

                foreach (var filter in this.filters)
                {
                    if (metric.MetricName.Contains(filter))
                    {
                        lock (lockList)
                        {
                            list.Add(metric);
                        }

                        break;
                    }
                }
            }
        }
    }
}
