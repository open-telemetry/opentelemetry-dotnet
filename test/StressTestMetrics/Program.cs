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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Metrics;

// namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static readonly Meter TestMeter = new Meter("TestMeter", "1.0.0");
    private static readonly Counter<long> TestCounter = TestMeter.CreateCounter<long>("TestCounter");
    private static readonly string[] DimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
    private static readonly ThreadLocal<Random> TlsRandom = new ThreadLocal<Random>(() => new Random());

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSource("TestMeter")
            .Build();
        Stress();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        var random = TlsRandom.Value;
        TestCounter.Add(
            100,
            new ("DimName1", DimensionValues[random.Next(0, 10)]),
            new ("DimName2", DimensionValues[random.Next(0, 10)]),
            new ("DimName3", DimensionValues[random.Next(0, 10)]));
    }
}
