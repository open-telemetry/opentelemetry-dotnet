// <copyright file="MyProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Logs;

internal class MyProcessor : LogProcessor
{
    private readonly string name;

    public MyProcessor(string name = "MyProcessor")
    {
        this.name = name;
    }

    public override void OnEnd(LogRecord record)
    {
        var state = record.State;

        if (state is IReadOnlyCollection<KeyValuePair<string, object>> dict)
        {
            var isUnstructuredLog = dict.Count == 1;

            if (isUnstructuredLog)
            {
                foreach (var entry in dict)
                {
                    Console.WriteLine($"{record.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ} {record.CategoryName}({record.LogLevel}, Id={record.EventId}): {entry.Value}");
                }
            }
            else
            {
                Console.WriteLine($"{record.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ} {record.CategoryName}({record.LogLevel}, Id={record.EventId}):");
                foreach (var entry in dict)
                {
                    if (string.Equals(entry.Key, "{OriginalFormat}", StringComparison.Ordinal))
                    {
                        Console.WriteLine($"    $format: {entry.Value}");
                        continue;
                    }

                    Console.WriteLine($"    {entry.Key}: {entry.Value}");
                }
            }

            if (record.Exception != null)
            {
                Console.WriteLine($"    $exception: {record.Exception}");
            }
        }
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        Console.WriteLine($"{this.name}.OnForceFlush({timeoutMilliseconds})");
        return true;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Console.WriteLine($"{this.name}.OnShutdown({timeoutMilliseconds})");
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        Console.WriteLine($"{this.name}.Dispose({disposing})");
    }
}
