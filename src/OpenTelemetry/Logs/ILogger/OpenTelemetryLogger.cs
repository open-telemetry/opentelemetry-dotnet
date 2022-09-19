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
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    internal sealed class OpenTelemetryLogger : ILogger
    {
        private readonly InstrumentationScope instrumentationScope;
        private readonly OpenTelemetryLoggerProvider iloggerProvider;
        private readonly LoggerProviderSdk? otelLoggerProvider;

        internal OpenTelemetryLogger(string categoryName, OpenTelemetryLoggerProvider provider)
        {
            Guard.ThrowIfNull(categoryName);
            Guard.ThrowIfNull(provider);

            this.instrumentationScope = new(categoryName);
            this.iloggerProvider = provider;
            this.otelLoggerProvider = provider.Provider as LoggerProviderSdk;
        }

        internal IExternalScopeProvider? ScopeProvider { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!this.IsEnabled(logLevel)
                || Sdk.SuppressInstrumentation)
            {
                return;
            }

            var iloggerProvider = this.iloggerProvider;
            var processor = this.otelLoggerProvider?.Processor;
            if (processor != null)
            {
                var activity = Activity.Current;

                var pool = this.otelLoggerProvider!.LogRecordPool;

                var record = pool.Rent();

                ref LogRecord.LogRecordILoggerData iloggerData = ref record.ILoggerData;

                iloggerData.TraceState = iloggerProvider.IncludeTraceState && activity != null
                    ? activity.TraceStateString
                    : null;
                iloggerData.CategoryName = this.instrumentationScope.Name;
                iloggerData.EventId = eventId;
                iloggerData.FormattedMessage = iloggerProvider.IncludeFormattedMessage ? formatter?.Invoke(state, exception) : null;
                iloggerData.Exception = exception;

                ref LogRecordData data = ref record.Data;

                data.TimestampBacking = DateTime.UtcNow;
                data.Severity = (LogRecordSeverity)logLevel;

                LogRecordData.SetActivityContext(ref data, activity);

                record.InstrumentationScope = this.instrumentationScope;

                var attributes = record.Attributes = iloggerProvider.IncludeAttributes
                    ? ProcessState(record, state, iloggerProvider.ParseStateValues)
                    : null;

                if (attributes != null)
                {
                    iloggerData.State = null;

                    if (attributes.Count > 0)
                    {
                        var lastAttribute = attributes[attributes.Count - 1];
                        data.Body = lastAttribute.Key == "{OriginalFormat}"
                            ? lastAttribute.Value as string
                            : null;
                    }
                    else
                    {
                        data.Body = null;
                    }
                }
                else
                {
                    iloggerData.State = !iloggerProvider.ParseStateValues ? state : null;
                }

                record.ScopeProvider = iloggerProvider.IncludeScopes ? this.ScopeProvider : null;
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

        private static IReadOnlyList<KeyValuePair<string, object?>>? ProcessState<TState>(LogRecord logRecord, TState state, bool parseStateValues)
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
                if (!parseStateValues || state is null)
                {
                    return null;
                }

                try
                {
                    PropertyDescriptorCollection itemProperties = TypeDescriptor.GetProperties(state);

                    var attributeStorage = logRecord.AttributeStorage ??= new List<KeyValuePair<string, object?>>(itemProperties.Count);

                    foreach (PropertyDescriptor? itemProperty in itemProperties)
                    {
                        if (itemProperty == null)
                        {
                            continue;
                        }

                        object? value = itemProperty.GetValue(state);
                        if (value == null)
                        {
                            continue;
                        }

                        attributeStorage.Add(new KeyValuePair<string, object?>(itemProperty.Name, value));
                    }

                    return attributeStorage;
                }
                catch (Exception parseException)
                {
                    OpenTelemetrySdkEventSource.Log.LoggerParseStateException<TState>(parseException);

                    return Array.Empty<KeyValuePair<string, object?>>();
                }
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
