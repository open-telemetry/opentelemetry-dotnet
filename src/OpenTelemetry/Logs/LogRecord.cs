// <copyright file="LogRecord.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores details about a log message.
/// </summary>
public sealed class LogRecord
{
    internal LogRecordData Data;
    internal LogRecordILoggerData ILoggerData;
    internal List<KeyValuePair<string, object?>>? AttributeStorage;
    internal List<object?>? ScopeStorage;
    internal int PoolReferenceCount = int.MaxValue;

    private static readonly Action<object?, List<object?>> AddScopeToBufferedList = (object? scope, List<object?> state) =>
    {
        state.Add(scope);
    };

    internal LogRecord()
    {
    }

    // Note: Some users are calling this with reflection. Try not to change the signature to be nice.
    [Obsolete("Call LogRecordPool.Rent instead.")]
    internal LogRecord(
        IExternalScopeProvider? scopeProvider,
        DateTime timestamp,
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string? formattedMessage,
        object? state,
        Exception? exception,
        IReadOnlyList<KeyValuePair<string, object?>>? stateValues)
    {
        var activity = Activity.Current;

        this.Data = new(activity)
        {
            TimestampBacking = timestamp,

            Body = formattedMessage,
        };

        OpenTelemetryLogger.SetLogRecordSeverityFields(ref this.Data, logLevel);

        this.ILoggerData = new()
        {
            TraceState = activity?.TraceStateString,
            CategoryName = categoryName,
            FormattedMessage = formattedMessage,
            EventId = eventId,
            Exception = exception,
            State = state,
            ScopeProvider = scopeProvider,
        };

        if (stateValues != null && stateValues.Count > 0)
        {
            var lastAttribute = stateValues[stateValues.Count - 1];
            if (lastAttribute.Key == "{OriginalFormat}"
               && lastAttribute.Value is string template)
            {
                this.Data.Body = template;
            }

            this.Attributes = stateValues;
        }
    }

    /// <summary>
    /// Gets or sets the log timestamp.
    /// </summary>
    /// <remarks>
    /// Note: If <see cref="Timestamp"/> is set to a value with <see
    /// cref="DateTimeKind.Local"/> it will be automatically converted to
    /// UTC using <see cref="DateTime.ToUniversalTime"/>.
    /// </remarks>
    public DateTime Timestamp
    {
        get => this.Data.Timestamp;
        set => this.Data.Timestamp = value;
    }

    /// <summary>
    /// Gets or sets the log <see cref="ActivityTraceId"/>.
    /// </summary>
    public ActivityTraceId TraceId
    {
        get => this.Data.TraceId;
        set => this.Data.TraceId = value;
    }

    /// <summary>
    /// Gets or sets the log <see cref="ActivitySpanId"/>.
    /// </summary>
    public ActivitySpanId SpanId
    {
        get => this.Data.SpanId;
        set => this.Data.SpanId = value;
    }

    /// <summary>
    /// Gets or sets the log <see cref="ActivityTraceFlags"/>.
    /// </summary>
    public ActivityTraceFlags TraceFlags
    {
        get => this.Data.TraceFlags;
        set => this.Data.TraceFlags = value;
    }

    /// <summary>
    /// Gets or sets the log trace state.
    /// </summary>
    /// <remarks>
    /// Note: Only set if <see
    /// cref="OpenTelemetryLoggerOptions.IncludeTraceState"/> is enabled.
    /// </remarks>
    public string? TraceState
    {
        get => this.ILoggerData.TraceState;
        set => this.ILoggerData.TraceState = value;
    }

    /// <summary>
    /// Gets or sets the log category name.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="CategoryName"/> is only set when emitting logs through <see cref="ILogger"/>.
    /// </remarks>
    public string? CategoryName
    {
        get => this.ILoggerData.CategoryName;
        set => this.ILoggerData.CategoryName = value;
    }

    /// <summary>
    /// Gets or sets the log <see cref="Microsoft.Extensions.Logging.LogLevel"/>.
    /// </summary>
#if EXPOSE_EXPERIMENTAL_FEATURES
    [Obsolete("Use Severity instead. LogLevel will be removed in a future version.")]
#endif
    public LogLevel LogLevel
    {
        get
        {
            if (this.Data.Severity.HasValue)
            {
                uint severity = (uint)this.Data.Severity.Value;
                if (severity >= 1 && severity <= 24)
                {
                    return (LogLevel)((severity - 1) / 4);
                }
            }

            return LogLevel.Trace;
        }

        set
        {
            OpenTelemetryLogger.SetLogRecordSeverityFields(ref this.Data, value);
        }
    }

    /// <summary>
    /// Gets or sets the log <see cref="Microsoft.Extensions.Logging.EventId"/>.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="EventId"/> is only set when emitting logs through <see cref="ILogger"/>.
    /// </remarks>
    public EventId EventId
    {
        get => this.ILoggerData.EventId;
        set => this.ILoggerData.EventId = value;
    }

    /// <summary>
    /// Gets or sets the log formatted message.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item><see cref="FormattedMessage"/> is only set when emitting logs
    /// through <see cref="ILogger"/>.</item>
    /// <item>Set if <see
    /// cref="OpenTelemetryLoggerOptions.IncludeFormattedMessage"/> is enabled
    /// or <c>{OriginalFormat}</c> attribute (message template) is not
    /// found.</item>
    /// </list>
    /// </remarks>
    public string? FormattedMessage
    {
        get => this.ILoggerData.FormattedMessage;
        set => this.ILoggerData.FormattedMessage = value;
    }

    /// <summary>
    /// Gets or sets the log body.
    /// </summary>
    /// <remarks>
    /// Note: Set to the <c>{OriginalFormat}</c> attribute (message
    /// template) if found otherwise the formatted log message.
    /// </remarks>
    public string? Body
    {
        get => this.Data.Body;
        set => this.Data.Body = value;
    }

    /// <summary>
    /// Gets or sets the raw state attached to the log.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item><see cref="State"/> is only set when emitting logs
    /// through <see cref="ILogger"/>.</item>
    /// <item>Set to <see langword="null"/> when <see
    /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled.</item>
    /// </list>
    /// </remarks>
    [Obsolete("State cannot be accessed safely outside of an ILogger.Log call stack. Use Attributes instead to safely access the data attached to a LogRecord. State will be removed in a future version.")]
    public object? State
    {
        get => this.ILoggerData.State;
        set => this.ILoggerData.State = value;
    }

    /// <summary>
    /// Gets or sets the state values attached to the log.
    /// </summary>
    /// <remarks><inheritdoc cref="Attributes" /></remarks>
    [Obsolete("Use Attributes instead. StateValues will be removed in a future version.")]
    public IReadOnlyList<KeyValuePair<string, object?>>? StateValues
    {
        get => this.Attributes;
        set => this.Attributes = value;
    }

    /// <summary>
    /// Gets or sets the attributes attached to the log.
    /// </summary>
    /// <remarks>
    /// Note: Set when <see
    /// cref="OpenTelemetryLoggerOptions.IncludeAttributes"/> is enabled and
    /// log record state implements <see cref="IReadOnlyList{T}"/> or <see
    /// cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/>s
    /// (where TKey is <c>string</c> and TValue is <c>object</c>) or <see
    /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled
    /// otherwise <see langword="null"/>.
    /// </remarks>
    public IReadOnlyList<KeyValuePair<string, object?>>? Attributes { get; set; }

    /// <summary>
    /// Gets or sets the log <see cref="System.Exception"/>.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="Exception"/> is only set when emitting logs through <see cref="ILogger"/>.
    /// </remarks>
    public Exception? Exception
    {
        get => this.ILoggerData.Exception;
        set => this.ILoggerData.Exception = value;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets or sets the original string representation of the severity as it is
    /// known at the source.
    /// </summary>
    /// <remarks><inheritdoc cref="Sdk.CreateLoggerProviderBuilder" path="/remarks"/></remarks>
    public
#else
    /// <summary>
    /// Gets or sets the original string representation of the severity as it is
    /// known at the source.
    /// </summary>
    internal
#endif
    string? SeverityText
    {
        get => this.Data.SeverityText;
        set => this.Data.SeverityText = value;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets or sets the log <see cref="LogRecordSeverity"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="Sdk.CreateLoggerProviderBuilder" path="/remarks"/></remarks>
    public
#else
    /// <summary>
    /// Gets or sets the log <see cref="LogRecordSeverity"/>.
    /// </summary>
    internal
#endif
        LogRecordSeverity? Severity
    {
        get => this.Data.Severity;
        set => this.Data.Severity = value;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets the <see cref="Logs.Logger"/> which emitted the <see cref="LogRecord"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="Sdk.CreateLoggerProviderBuilder" path="/remarks"/></remarks>
    public Logger? Logger { get; internal set; }
#else
    /// <summary>
    /// Gets or sets the <see cref="Logs.Logger"/> which emitted the <see cref="LogRecord"/>.
    /// </summary>
    internal Logger? Logger { get; set; }
#endif

    /// <summary>
    /// Executes callback for each currently active scope objects in order
    /// of creation. All callbacks are guaranteed to be called inline from
    /// this method.
    /// </summary>
    /// <typeparam name="TState">State.</typeparam>
    /// <param name="callback">The callback to be executed for every scope object.</param>
    /// <param name="state">The state object to be passed into the callback.</param>
    public void ForEachScope<TState>(Action<LogRecordScope, TState> callback, TState state)
    {
        Guard.ThrowIfNull(callback);

        var forEachScopeState = new ScopeForEachState<TState>(callback, state);

        var bufferedScopes = this.ILoggerData.BufferedScopes;
        if (bufferedScopes != null)
        {
            foreach (object? scope in bufferedScopes)
            {
                ScopeForEachState<TState>.ForEachScope(scope, forEachScopeState);
            }
        }
        else
        {
            this.ILoggerData.ScopeProvider?.ForEachScope(ScopeForEachState<TState>.ForEachScope, forEachScopeState);
        }
    }

    /// <summary>
    /// Gets a reference to the <see cref="LogRecordData"/> for the log message.
    /// </summary>
    /// <returns><see cref="LogRecordData"/>.</returns>
    internal ref LogRecordData GetDataRef()
    {
        return ref this.Data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetReferenceCount()
    {
        this.PoolReferenceCount = 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddReference()
    {
        Interlocked.Increment(ref this.PoolReferenceCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int RemoveReference()
    {
        return Interlocked.Decrement(ref this.PoolReferenceCount);
    }

    // Note: Typically called when LogRecords are added into a batch so they
    // can be safely processed outside of the log call chain.
    internal void Buffer()
    {
        // Note: Attributes are buffered because some states are not safe to
        // access outside of the log call chain. See:
        // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2905
        this.BufferLogAttributes();

        this.BufferLogScopes();
    }

    internal LogRecord Copy()
    {
        // Note: We only buffer scopes here because attributes are copied
        // directly below.
        this.BufferLogScopes();

        return new()
        {
            Data = this.Data,
            ILoggerData = this.ILoggerData.Copy(),
            Attributes = this.Attributes == null ? null : new List<KeyValuePair<string, object?>>(this.Attributes),
            Logger = this.Logger,
        };
    }

    /// <summary>
    /// Buffers the attributes attached to the log into a list so that they
    /// can be safely processed after the log message lifecycle has ended.
    /// </summary>
    private void BufferLogAttributes()
    {
        var attributes = this.Attributes;
        if (attributes == null || attributes == this.AttributeStorage)
        {
            return;
        }

        var attributeStorage = this.AttributeStorage ??= new List<KeyValuePair<string, object?>>(attributes.Count);

        // Note: AddRange here will copy all of the KeyValuePairs from
        // attributes to AttributeStorage. This "captures" the state and
        // fixes issues where the values are generated at enumeration time
        // like
        // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2905.
        attributeStorage.AddRange(attributes);

        this.Attributes = attributeStorage;
    }

    /// <summary>
    /// Buffers the scopes attached to the log into a list so that they can
    /// be safely processed after the log message lifecycle has ended.
    /// </summary>
    private void BufferLogScopes()
    {
        var scopeProvider = this.ILoggerData.ScopeProvider;
        if (scopeProvider == null)
        {
            return;
        }

        var scopeStorage = this.ScopeStorage ??= new List<object?>(LogRecordPoolHelper.DefaultMaxNumberOfScopes);

        scopeProvider.ForEachScope(AddScopeToBufferedList, scopeStorage);

        this.ILoggerData.ScopeProvider = null;

        this.ILoggerData.BufferedScopes = scopeStorage;
    }

    internal struct LogRecordILoggerData
    {
        public string? TraceState;
        public string? CategoryName;
        public EventId EventId;
        public string? FormattedMessage;
        public Exception? Exception;
        public object? State;
        public IExternalScopeProvider? ScopeProvider;
        public List<object?>? BufferedScopes;

        public LogRecordILoggerData Copy()
        {
            var copy = new LogRecordILoggerData
            {
                TraceState = this.TraceState,
                CategoryName = this.CategoryName,
                EventId = this.EventId,
                FormattedMessage = this.FormattedMessage,
                Exception = this.Exception,
                State = this.State,
            };

            var bufferedScopes = this.BufferedScopes;
            if (bufferedScopes != null)
            {
                copy.BufferedScopes = new List<object?>(bufferedScopes);
            }

            return copy;
        }
    }

    private readonly struct ScopeForEachState<TState>
    {
        public static readonly Action<object?, ScopeForEachState<TState>> ForEachScope = (object? scope, ScopeForEachState<TState> state) =>
        {
            LogRecordScope logRecordScope = new LogRecordScope(scope);

            state.Callback(logRecordScope, state.UserState);
        };

        public readonly Action<LogRecordScope, TState> Callback;

        public readonly TState UserState;

        public ScopeForEachState(Action<LogRecordScope, TState> callback, TState state)
        {
            this.Callback = callback;
            this.UserState = state;
        }
    }
}
