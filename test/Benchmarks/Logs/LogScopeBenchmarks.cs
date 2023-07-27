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

namespace Benchmarks.Logs;

public class LogScopeBenchmarks
{
    private readonly LoggerExternalScopeProvider scopeProvider = new();

    private readonly Action<LogRecordScope, object> callback = (LogRecordScope scope, object state) =>
    {
        foreach (KeyValuePair<string, object> scopeItem in scope)
        {
        }
    };

    private readonly LogRecord logRecord;

    public LogScopeBenchmarks()
    {
        this.scopeProvider.Push(new ReadOnlyCollection<KeyValuePair<string, object>>(
            new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("item1", "value1"),
                new KeyValuePair<string, object>("item2", "value2"),
            }));
        this.scopeProvider.Push(new ReadOnlyCollection<KeyValuePair<string, object>>(
            new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("item3", "value3"),
            }));
        this.scopeProvider.Push(new ReadOnlyCollection<KeyValuePair<string, object>>(
            new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("item4", "value4"),
                new KeyValuePair<string, object>("item5", "value5"),
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
