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

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
                var activity = Activity.Current;

                var pool = provider.LogRecordPool;

                var record = pool.Rent();

                ref LogRecord.LogRecordILoggerData iloggerData = ref record.ILoggerData;

                iloggerData.TraceState = provider.IncludeTraceState && activity != null
                    ? activity.TraceStateString
                    : null;
                iloggerData.CategoryName = this.categoryName;
                iloggerData.EventId = eventId;
                iloggerData.LogLevel = logLevel;
                iloggerData.Exception = exception;
                iloggerData.ScopeProvider = provider.IncludeScopes ? this.ScopeProvider : null;
                iloggerData.BufferedScopes = null;

                ref LogRecordData data = ref record.Data;

                data.TimestampBacking = DateTime.UtcNow;

                LogRecordData.SetActivityContext(ref data, activity);

                var attributes = record.Attributes = provider.IncludeAttributes
                    ? ProcessState(record, ref iloggerData, in state, provider.ParseStateValues)
                    : null;

                if (!TryGetOriginalFormatFromAttributes(attributes, out var originalFormat))
                {
                    var formattedMessage = formatter?.Invoke(state, exception) ?? state?.ToString();

                    data.Body = formattedMessage;
                    iloggerData.FormattedMessage = formattedMessage;
                }
                else
                {
                    data.Body = originalFormat;
                    iloggerData.FormattedMessage = provider.IncludeFormattedMessage
                        ? formatter?.Invoke(state, exception) ?? state?.ToString()
                        : null;
                }

                processor.OnEnd(record);

                iloggerData.ScopeProvider = null;

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

        private static IReadOnlyList<KeyValuePair<string, object?>>? ProcessState<TState>(
            LogRecord logRecord,
            ref LogRecord.LogRecordILoggerData iLoggerData,
            in TState state,
            bool parseStateValues)
        {
            iLoggerData.State = null;

            /* TODO: Enable this if/when LogRecordAttributeList becomes public.
            if (typeof(TState) == typeof(LogRecordAttributeList))
            {
                // Note: This cast looks strange, but it is meant for the JIT to optimize/remove.
                ((LogRecordAttributeList)(object)state!).ApplyToLogRecord(logRecord);
                return logRecord.AttributeStorage!;
            }
            else */
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
            else if (!parseStateValues || state is null)
            {
                // Note: This is to preserve legacy behavior where State is
                // exposed if we didn't parse state.
                iLoggerData.State = state;
                return null;
            }
            else
            {
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

        private static bool TryGetOriginalFormatFromAttributes(
            IReadOnlyList<KeyValuePair<string, object?>>? attributes,
            [NotNullWhen(true)] out string? originalFormat)
        {
            if (attributes != null && attributes.Count > 0)
            {
                var lastAttribute = attributes[attributes.Count - 1];
                if (lastAttribute.Key == "{OriginalFormat}"
                    && lastAttribute.Value is string tempOriginalFormat)
                {
                    originalFormat = tempOriginalFormat;
                    return true;
                }
            }

            originalFormat = null;
            return false;
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
