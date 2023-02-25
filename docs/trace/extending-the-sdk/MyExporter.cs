// <copyright file="MyExporter.cs" company="OpenTelemetry Authors">
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
using System.Text;
using OpenTelemetry;

internal class MyExporter : BaseExporter<Activity>
{
    private readonly string name;

    public MyExporter(string name = "MyExporter")
    {
        this.name = name;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        // SuppressInstrumentationScope should be used to prevent exporter
        // code from generating telemetry and causing live-loop.
        using var scope = SuppressInstrumentationScope.Begin();

        var sb = new StringBuilder();
        foreach (var activity in batch)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(activity.DisplayName);
        }

        Console.WriteLine($"{this.name}.Export([{sb}])");
        return ExportResult.Success;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Console.WriteLine($"{this.name}.OnShutdown(timeoutMilliseconds={timeoutMilliseconds})");
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        Console.WriteLine($"{this.name}.Dispose({disposing})");
    }
}
