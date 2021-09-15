// <copyright file="MyReader.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Metrics;

internal class MyReader : MetricReader
{
    private readonly string name;

    public MyReader(string name = "MyReader")
    {
        this.name = name;
    }

    public override void OnCollect(Batch<Metric> metrics)
    {
        Console.WriteLine($"{this.name}.OnCollect({metrics})");
    }

    protected override void Dispose(bool disposing)
    {
        Console.WriteLine($"{this.name}.Dispose({disposing})");
    }
}
