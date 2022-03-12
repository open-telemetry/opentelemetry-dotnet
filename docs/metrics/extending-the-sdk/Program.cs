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

namespace ExtendingTheSdk;

public class Program
{
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("MyFruitCounter");

    static Program()
    {
        var process = Process.GetCurrentProcess();

        MyMeter.CreateObservableGauge(
            "MyProcessWorkingSetGauge",
            () => new List<Measurement<long>>()
            {
                new(process.WorkingSet64, new("process.id", process.Id), new("process.bitness", IntPtr.Size << 3)),
            });
    }

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("MyCompany.MyProduct.MyLibrary")
            .AddReader(new BaseExportingMetricReader(new MyExporter("ExporterX")))
            .AddMyExporter()
            .Build();

        MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
        MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
        MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
        MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
        MyFruitCounter.Add(5, new("name", "apple"), new("color", "red"));
        MyFruitCounter.Add(4, new("name", "lemon"), new("color", "yellow"));
    }
}
