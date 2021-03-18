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

#if NET461 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    internal class OpenTelemetryLogger : ILogger
    {
        private readonly string categoryName;
        private readonly OpenTelemetryLoggerProvider provider;

        internal OpenTelemetryLogger(string categoryName, OpenTelemetryLoggerProvider provider)
        {
            this.categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!this.IsEnabled(logLevel)
                || Sdk.SuppressInstrumentation)
            {
                return;
            }

            var processor = this.provider.Processor;
            if (processor != null)
            {
                var options = this.provider.Options;

                var record = new LogRecord(
                    options.IncludeScopes ? this.ScopeProvider : null,
                    DateTime.UtcNow,
                    this.categoryName,
                    logLevel,
                    eventId,
                    options.IncludeFormattedMessage ? formatter(state, exception) : null,
                    options.ParseStateValues ? null : (object)state,
                    exception,
                    options.ParseStateValues ? this.ParseState(state) : null);

                processor.OnEnd(record);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public IDisposable BeginScope<TState>(TState state) => this.ScopeProvider?.Push(state) ?? null;

        private IReadOnlyList<KeyValuePair<string, object>> ParseState<TState>(TState state)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object>> stateList)
            {
                return stateList;
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> stateValues)
            {
                return new List<KeyValuePair<string, object>>(stateValues);
            }
            else
            {
                return new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>(string.Empty, state),
                };
            }
        }
    }
}
#endif
