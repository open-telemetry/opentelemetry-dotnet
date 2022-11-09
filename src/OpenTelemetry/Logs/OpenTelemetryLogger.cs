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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    internal sealed class OpenTelemetryLogger : ILogger
    {
        private readonly string categoryName;
        private readonly OpenTelemetryLoggerProvider provider;

        internal OpenTelemetryLogger(string categoryName, OpenTelemetryLoggerProvider provider)
        {
            Guard.ThrowIfNull(categoryName);
            Guard.ThrowIfNull(provider);

            this.categoryName = categoryName;
            this.provider = provider;
        }

        internal IExternalScopeProvider? ScopeProvider { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!this.IsEnabled(logLevel)
                || Sdk.SuppressInstrumentation)
            {
                return;
            }

            var provider = this.provider;
            var processor = provider.Processor;
            if (processor != null)
            {
                var pool = provider.LogRecordPool;

                var record = pool.Rent();

                record.ScopeProvider = provider.IncludeScopes ? this.ScopeProvider : null;
                record.State = provider.ParseStateValues ? null : state;
                record.StateValues = provider.ParseStateValues ? ParseState(record, state) : null;
                record.BufferedScopes = null;

                ref LogRecordData data = ref record.Data;

                data.TimestampBacking = DateTime.UtcNow;
                data.CategoryName = this.categoryName;
                data.LogLevel = logLevel;
                data.EventId = eventId;
                data.Message = provider.IncludeFormattedMessage ? formatter?.Invoke(state, exception) : null;
                data.Exception = exception;

                LogRecordData.SetActivityContext(ref data, Activity.Current);

                processor.OnEnd(record);

                record.ScopeProvider = null;

                // Attempt to return the LogRecord to the pool. This will no-op
                // if a batch exporter has added a reference.
                pool.Return(record);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public IDisposable BeginScope<TState>(TState state) => this.ScopeProvider?.Push(state) ?? NullScope.Instance;

        private static IReadOnlyList<KeyValuePair<string, object?>> ParseState<TState>(LogRecord logRecord, TState state)
        {
            /* TODO: Enable this if/when LogRecordAttributeList becomes public.
            if (state is LogRecordAttributeList logRecordAttributes)
            {
                logRecordAttributes.ApplyToLogRecord(logRecord);
                return logRecord.AttributeStorage!;
            }
            else*/
            if (state is IReadOnlyList<KeyValuePair<string, object?>> stateList)
            {
                return stateList;
            }
            else if (state is IEnumerable<KeyValuePair<string, object?>> stateValues)
            {
                var attributeStorage = logRecord.AttributeStorage;
                if (attributeStorage == null)
                {
                    return logRecord.AttributeStorage = new List<KeyValuePair<string, object?>>(stateValues);
                }
                else
                {
                    attributeStorage.AddRange(stateValues);
                    return attributeStorage;
                }
            }
            else
            {
                var attributeStorage = logRecord.AttributeStorage ??= new List<KeyValuePair<string, object?>>(LogRecordPoolHelper.DefaultMaxNumberOfAttributes);
                attributeStorage.Add(new KeyValuePair<string, object?>(string.Empty, state));
                return attributeStorage;
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
