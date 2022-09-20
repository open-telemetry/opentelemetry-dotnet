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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Stores details about a log message.
    /// </summary>
    public sealed class LogRecord
    {
        internal LogRecordData Data;
        internal LogRecordILoggerData ILoggerData;
        internal List<KeyValuePair<string, object?>>? AttributeStorage;
        internal List<object?>? BufferedScopes;
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
                Severity = (LogRecordSeverity)logLevel,
            };

            this.ILoggerData = new()
            {
                TraceState = activity?.TraceStateString,
                CategoryName = categoryName,
                FormattedMessage = formattedMessage,
                EventId = eventId,
                Exception = exception,
                State = state,
            };

            if (stateValues != null && stateValues.Count > 0)
            {
                var lastAttribute = stateValues[stateValues.Count - 1];
                this.Data.Body = lastAttribute.Key == "{OriginalFormat}"
                   ? lastAttribute.Value as string
                   : null;
            }

            this.Data.Body ??= formattedMessage;

            this.InstrumentationScope = null;

            this.Attributes = stateValues;

            this.ScopeProvider = scopeProvider;
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
        /// Gets or sets the log trace state. Only set if <see
        /// cref="OpenTelemetryLoggerOptions.IncludeTraceState"/> is enabled.
        /// </summary>
        /// <remarks>
        /// Note: <see cref="TraceState"/> is only set when emitting logs through <see cref="ILogger"/>.
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
        // [Obsolete("Use Severity instead LogLevel will be removed in a future version.")]
        public LogLevel LogLevel
        {
            get => (LogLevel)this.Data.Severity;
            set => this.Data.Severity = (LogRecordSeverity)value;
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
        /// Gets or sets the log formatted message. Only set if <see
        /// cref="OpenTelemetryLoggerOptions.IncludeFormattedMessage"/> is enabled.
        /// </summary>
        /// <remarks>
        /// Note: <see cref="FormattedMessage"/> is only set when emitting logs through <see cref="ILogger"/>.
        /// </remarks>
        public string? FormattedMessage
        {
            get => this.ILoggerData.FormattedMessage;
            set => this.ILoggerData.FormattedMessage = value;
        }

        /// <summary>
        /// Gets or sets the log body.
        /// </summary>
        public string? Body
        {
            get => this.Data.Body;
            set => this.Data.Body = value;
        }

        /// <summary>
        /// Gets or sets the raw state attached to the log. Set to <see
        /// langword="null"/> when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled.
        /// </summary>
        /// <remarks>
        /// Note: <see cref="State"/> is only set when emitting logs through <see cref="ILogger"/>.
        /// </remarks>
        [Obsolete("State cannot be accessed safely outside of an ILogger.Log call stack. It will be removed in a future version.")]
        public object? State
        {
            get => this.ILoggerData.State;
            set => this.ILoggerData.State = value;
        }

        /// <summary>
        /// Gets or sets the parsed state values attached to the log. Set when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled
        /// otherwise <see langword="null"/>.
        /// </summary>
        [Obsolete("Use Attributes instead StateValues will be removed in a future version.")]
        public IReadOnlyList<KeyValuePair<string, object?>>? StateValues
        {
            get => this.Attributes;
            set => this.Attributes = value;
        }

        /// <summary>
        /// Gets or sets the attributes attached to the log.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the log <see cref="LogRecordSeverity"/>.
        /// </summary>
        internal LogRecordSeverity Severity
        {
            get => this.Data.Severity;
            set => this.Data.Severity = value;
        }

        /// <summary>
        /// Gets or sets the log <see cref="OpenTelemetry.InstrumentationScope"/>.
        /// </summary>
        internal InstrumentationScope? InstrumentationScope { get; set; }

        internal IExternalScopeProvider? ScopeProvider { get; set; }

        /// <summary>
        /// Executes callback for each currently active scope objects in order
        /// of creation. All callbacks are guaranteed to be called inline from
        /// this method.
        /// </summary>
        /// <typeparam name="TState">State.</typeparam>
        /// <remarks>
        /// Note: Scopes are only supported when emitting logs through <see cref="ILogger"/>.
        /// </remarks>
        /// <param name="callback">The callback to be executed for every scope object.</param>
        /// <param name="state">The state object to be passed into the callback.</param>
        public void ForEachScope<TState>(Action<LogRecordScope, TState> callback, TState state)
        {
            Guard.ThrowIfNull(callback);

            var forEachScopeState = new ScopeForEachState<TState>(callback, state);

            if (this.BufferedScopes != null)
            {
                foreach (object? scope in this.BufferedScopes)
                {
                    ScopeForEachState<TState>.ForEachScope(scope, forEachScopeState);
                }
            }
            else
            {
                this.ScopeProvider?.ForEachScope(ScopeForEachState<TState>.ForEachScope, forEachScopeState);
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
            // Note: Attributes are buffered because some states are not safe
            // to access outside of the log call chain. See:
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2905
            this.BufferLogAttributes();

            this.BufferLogScopes();
        }

        internal LogRecord Copy()
        {
            // Note: We only buffer scopes here because state values are copied
            // directly below.
            this.BufferLogScopes();

            return new()
            {
                Data = this.Data,
                ILoggerData = this.ILoggerData,
                Attributes = this.Attributes == null ? null : new List<KeyValuePair<string, object?>>(this.Attributes),
                BufferedScopes = this.BufferedScopes == null ? null : new List<object?>(this.BufferedScopes),
            };
        }

        /// <summary>
        /// Buffers the state values attached to the log into a list so that
        /// they can be safely processed after the log message lifecycle has
        /// ended.
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
            // stateValues to AttributeStorage. This "captures" the state and
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
            if (this.ScopeProvider == null)
            {
                return;
            }

            List<object?> scopes = this.BufferedScopes ??= new List<object?>(LogRecordPoolHelper.DefaultMaxNumberOfScopes);

            this.ScopeProvider.ForEachScope(AddScopeToBufferedList, scopes);

            this.ScopeProvider = null;
        }

        internal struct LogRecordILoggerData
        {
            public string? TraceState;
            public string? CategoryName;
            public EventId EventId;
            public string? FormattedMessage;
            public Exception? Exception;
            public object? State;
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
}
