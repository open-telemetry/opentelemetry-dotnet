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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace LearningMoreInstruments;

public class Program
{
    private static readonly Meter MyMeter = new("FruitCompany.FruitSales", "1.0");
    private static readonly Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("FruitsSold");
    private static readonly Histogram<long> MyFruitSalePrice = MyMeter.CreateHistogram<long>("FruitSalePrice");

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MyMeter.Name)
            .AddConsoleExporter()
            .AddOtlpExporter((exporterOptions, metricReaderOptions) => exporterOptions.Endpoint = new Uri("http://localhost:4317"))
            .Build();

        var rand = new Random();

        for (int i = 0; i < 1000; i++)
        {
            using (var act = new Activity("Test").Start())
            {
                MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
                MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
                MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
                MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
                MyFruitCounter.Add(5, new("name", "apple"), new("color", "red"));
                MyFruitCounter.Add(4, new("name", "lemon"), new("color", "yellow"));

                MyFruitSalePrice.Record(rand.Next(1, 1000), new("name", "apple"), new("color", "red"));
                MyFruitSalePrice.Record(rand.Next(1, 1000), new("name", "lemon"), new("color", "yellow"));
                MyFruitSalePrice.Record(rand.Next(1, 1000), new("name", "lemon"), new("color", "yellow"));
                MyFruitSalePrice.Record(rand.Next(1, 1000), new("name", "apple"), new("color", "green"));
                MyFruitSalePrice.Record(rand.Next(1, 1000), new("name", "apple"), new("color", "red"));
                MyFruitSalePrice.Record(rand.Next(1, 1000), new("name", "lemon"), new("color", "yellow"));
            }
        }
    }
}
