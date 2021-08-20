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
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

public class Program
{
    private static readonly Meter MyMeter = new Meter("TestMeter", "0.0.1");

    public static async Task Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddConsoleExporter((option) =>
                {
                    option.IsDelta = false;
                })
                .Build();

        var observableCounter = MyMeter.CreateObservableCounter<long>(
            "observable-counter",
            () =>
            {
                var tag1 = new KeyValuePair<string, object>("tag1", "value1");
                var tag2 = new KeyValuePair<string, object>("tag2", "value2");

                return new List<Measurement<long>>()
                {
                    // Report an absoulute value (not an increment/delta value).
                    new Measurement<long>(10, tag1, tag2),
                };
            });

        // Do other stuff...
        await Task.Delay(10000);
    }
}
