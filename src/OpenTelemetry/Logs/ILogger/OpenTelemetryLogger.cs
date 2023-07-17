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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetryLogger : ILogger
{
    private static readonly string[] LogLevels = new string[]
    {
        nameof(LogLevel.Trace),
        nameof(LogLevel.Debug),
        nameof(LogLevel.Information),
        nameof(LogLevel.Warning),
        nameof(LogLevel.Error),
        nameof(LogLevel.Critical),
        nameof(LogLevel.None),
    };

    private readonly LoggerProviderSdk provider;
    private readonly OpenTelemetryLoggerOptions options;
    private readonly string categoryName;

    internal OpenTelemetryLogger(
        LoggerProviderSdk provider,
        OpenTelemetryLoggerOptions options,
        string categoryName)
    {
        Debug.Assert(provider != null, "provider was null");
        Debug.Assert(categoryName != null, "categoryName was null");
        Debug.Assert(options != null, "options was null");

        this.provider = provider!;
        this.options = options!;
        this.categoryName = categoryName!;
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
        var processor = this.provider.Processor;
        if (processor != null)
        {
            var activity = Activity.Current;

            var pool = this.provider.LogRecordPool;

            var record = pool.Rent();

            ref LogRecord.LogRecordILoggerData iloggerData = ref record.ILoggerData;

            iloggerData.TraceState = this.options.IncludeTraceState && activity != null
                ? activity.TraceStateString
                : null;
            iloggerData.CategoryName = this.categoryName;
            iloggerData.EventId = eventId;
            iloggerData.Exception = exception;
            iloggerData.ScopeProvider = this.options.IncludeScopes ? this.ScopeProvider : null;
            iloggerData.BufferedScopes = null;

            ref LogRecordData data = ref record.Data;

            data.TimestampBacking = DateTime.UtcNow;

            SetLogRecordSeverityFields(ref data, logLevel);

            LogRecordData.SetActivityContext(ref data, activity);

            var attributes = record.Attributes =
                ProcessState(record, ref iloggerData, in state, this.options.IncludeAttributes, this.options.ParseStateValues);

            if (!TryGetOriginalFormatFromAttributes(attributes, out var originalFormat))
            {
                var formattedMessage = formatter?.Invoke(state, exception) ?? state?.ToString();

                data.Body = formattedMessage;
                iloggerData.FormattedMessage = formattedMessage;
            }
            else
            {
                data.Body = originalFormat;
                iloggerData.FormattedMessage = this.options.IncludeFormattedMessage
                    ? formatter?.Invoke(state, exception) ?? state?.ToString()
                    : null;
            }

            record.Logger = LoggerInstrumentationScope.Instance;

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

    internal static void SetLogRecordSeverityFields(ref LogRecordData logRecordData, LogLevel logLevel)
    {
        uint intLogLevel = (uint)logLevel;
        if (intLogLevel < 6)
        {
            logRecordData.Severity = (LogRecordSeverity)((intLogLevel * 4) + 1);
            logRecordData.SeverityText = LogLevels[intLogLevel];
        }
        else
        {
            logRecordData.Severity = null;
            logRecordData.SeverityText = null;
        }
    }

    private static IReadOnlyList<KeyValuePair<string, object?>>? ProcessState<TState>(
        LogRecord logRecord,
        ref LogRecord.LogRecordILoggerData iLoggerData,
        in TState state,
        bool includeAttributes,
        bool parseStateValues)
    {
        if (!includeAttributes
            || (!typeof(TState).IsValueType && state is null))
        {
            iLoggerData.State = null;
            return null;
        }

        if (typeof(TState) == typeof(LogRecordAttributeList))
        {
            // Note: This block is written to be elided by the JIT when
            // TState is not LogRecordAttributeList or optimized when it is.
            // For users that pass LogRecordAttributeList as TState to
            // ILogger.Log this will avoid boxing the struct.

            var logRecordAttributes = (LogRecordAttributeList)(object)state!;

            var exportedAttributes = logRecordAttributes.Export(ref logRecord.AttributeStorage);

            // Note: This is to preserve legacy behavior where State is exposed
            // if we didn't parse state. We use exportedAttributes here to prevent a
            // boxing of struct LogRecordAttributeList.
            iLoggerData.State = !parseStateValues ? exportedAttributes : null;

            return exportedAttributes;
        }
        else if (state is IReadOnlyList<KeyValuePair<string, object?>> stateList)
        {
            // Note: This is to preserve legacy behavior where State is exposed
            // if we didn't parse state. We use stateList here to prevent a
            // second boxing of struct TStates.
            iLoggerData.State = !parseStateValues ? stateList : null;

            return stateList;
        }
        else if (state is IEnumerable<KeyValuePair<string, object?>> stateValues)
        {
            // Note: This is to preserve legacy behavior where State is exposed
            // if we didn't parse state. We use stateValues here to prevent a
            // second boxing of struct TStates.
            iLoggerData.State = !parseStateValues ? stateValues : null;

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
        else if (!parseStateValues)
        {
            // Note: This is to preserve legacy behavior where State is
            // exposed if we didn't parse state.
            iLoggerData.State = state;

            return null;
        }
        else
        {
            // Note: We clear State because the LogRecord we are processing may
            // have come from the pool and may have State from a prior log.
            iLoggerData.State = null;

            OpenTelemetrySdkEventSource.Log.LoggerProcessStateSkipped<TState>();

            return Array.Empty<KeyValuePair<string, object?>>();
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

    private sealed class LoggerInstrumentationScope : Logger
    {
        private LoggerInstrumentationScope(string name, string version)
            : base(name)
        {
            this.SetInstrumentationScope(version);
        }

        public static LoggerInstrumentationScope Instance { get; }
            = new("OpenTelemetry", Sdk.InformationalVersion);

        public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
            => throw new NotSupportedException();
    }
}
