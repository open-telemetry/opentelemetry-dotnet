// <copyright file="LogScopeBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Collections.ObjectModel;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method       | Mean     | Error    | StdDev   | Allocated |
|------------- |---------:|---------:|---------:|----------:|
| ForEachScope | 74.29 ns | 0.605 ns | 0.536 ns |         - |
*/

namespace Benchmarks.Logs;

public class LogScopeBenchmarks
{
    private readonly LoggerExternalScopeProvider scopeProvider = new();

    private readonly Action<LogRecordScope, object> callback = (LogRecordScope scope, object state) =>
    {
        foreach (var scopeItem in scope)
        {
            _ = scopeItem.Key;
            _ = scopeItem.Value;
        }
    };

    private readonly LogRecord logRecord;

    public LogScopeBenchmarks()
    {
        this.scopeProvider.Push(new ReadOnlyCollection<KeyValuePair<string, object>>(
            new List<KeyValuePair<string, object>>
            {
                new("item1", "value1"),
                new("item2", "value2"),
            }));

        this.scopeProvider.Push(new ReadOnlyCollection<KeyValuePair<string, object>>(
            new List<KeyValuePair<string, object>>
            {
                new("item3", "value3"),
            }));

        this.scopeProvider.Push(new ReadOnlyCollection<KeyValuePair<string, object>>(
            new List<KeyValuePair<string, object>>
            {
                new("item4", "value4"),
                new("item5", "value5"),
            }));

#pragma warning disable CS0618 // Type or member is obsolete
        this.logRecord = new LogRecord(
            this.scopeProvider,
            DateTime.UtcNow,
            "Benchmark",
            LogLevel.Information,
            0,
            "Message",
            null,
            null,
            null);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Benchmark]
    public void ForEachScope()
    {
        this.logRecord.ForEachScope(this.callback, null);
    }
}
