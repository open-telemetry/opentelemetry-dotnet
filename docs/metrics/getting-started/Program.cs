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
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;

public class Program
{
    private static readonly Meter MyMeter = new Meter("TestMeter", "0.0.1");
    private static readonly Counter<long> Counter = MyMeter.CreateCounter<long>("counter");

    public static async Task Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddConsoleExporter()
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
                Task.Delay(10).Wait();
            }
        });
        writeMetricTask.Start();

        token.CancelAfter(10000);
        await writeMetricTask;
    }
}
