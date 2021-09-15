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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

public class Program
{
    private static readonly Meter MyMeter = new Meter("MyCompany.MyProduct.MyLibrary", "1.0");

    public static void Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddMetricReader(new MyReader())
            /** /
            TODO: revisit once this exception is removed "System.InvalidOperationException: Only one Metricreader is allowed.".
            .AddMetricReader(new BaseExportingMetricReader(new MyExporter()))
            /**/
            .Build();

        var process = Process.GetCurrentProcess();
        MyMeter.CreateObservableGauge<long>(
            "MyGauge",
            () => new List<Measurement<long>>()
            {
                new(process.WorkingSet64, new("process.id", process.Id), new("process.bitness", IntPtr.Size << 3)),
            });

        var counter = MyMeter.CreateCounter<long>("MyCounter");

        counter.Add(1, new("tag1", "value1"), new("tag2", "value2"));
        counter.Add(2, new("tag1", "value1"), new("tag2", "value2"));
    }
}
