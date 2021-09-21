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

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Logs;

public class CredScanExporter : BaseExporter<LogRecord>
{
    private readonly string name;
    private readonly Regex m_rules = new Regex(@"(?i)sig=[a-z0-9%]{43,63}%3d");

    public CredScanExporter(string name = "MyExporter")
    {
        this.name = name;
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        // SuppressInstrumentationScope should be used to prevent exporter
        // code from generating telemetry and causing live-loop.
        using var scope = SuppressInstrumentationScope.Begin();
        foreach (var logRecord in batch)
        {
            var listKvp = logRecord.State as IReadOnlyList<KeyValuePair<string, object>>;

            for (int i = 0; i < listKvp.Count; ++i)
            {
                var entry = listKvp[i];

                if (entry.Key == "{OriginalFormat}")
                {
                    continue;
                }

                var str = entry.Value as string; // if the value is not a string, we don't attempt to call ToString

                if (str != null)
                {
                    Console.WriteLine(str);
                    if (m_rules.IsMatch(str))
                    {
                        Console.WriteLine("such a sad story!");
                    }
                    else
                    {
                        Console.WriteLine("happy ending!");
                    }
                }
            }
        }

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
