// <copyright file="OpenTelemetryLogger.cs" company="OpenTelemetry Authors">
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

#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    internal class OpenTelemetryLogger : ILogger
    {
        private readonly string categoryName;

        internal OpenTelemetryLogger(string categoryName, OpenTelemetryLoggerOptions options)
        {
            this.categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
        }

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            var timestamp = DateTime.UtcNow;

            if (state is IReadOnlyCollection<KeyValuePair<string, object>> dict)
            {
                var isUnstructuredLog = dict.Count == 1;

                // TODO: remove the console output after finished the plumbing work to log processors/exporters
                if (isUnstructuredLog)
                {
                    foreach (var entry in dict)
                    {
                        Console.WriteLine($"{this.categoryName}({logLevel}, Id={eventId}): {entry.Value}");
                    }
                }
                else
                {
                    Console.WriteLine($"{this.categoryName}({logLevel}, Id={eventId}):");
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

                if (exception != null)
                {
                    Console.WriteLine($"    $exception: {exception}");
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public IDisposable BeginScope<TState>(TState state) => this.ScopeProvider?.Push(state) ?? null;
    }
}
#endif
