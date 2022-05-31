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
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Stores details about a log message.
    /// </summary>
    public sealed class LogRecord
    {
        internal int PoolReferences = int.MaxValue;
        internal LogRecordData Data;
        internal List<KeyValuePair<string, object?>>? AttributeStorage;
        internal List<object?>? BufferedScopes;

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
            this.Data = new(Activity.Current)
            {
                TimestampBacking = timestamp,

                CategoryName = categoryName,
                LogLevel = logLevel,
                EventId = eventId,
                Message = formattedMessage,
                Exception = exception,
            };

            this.ScopeProvider = scopeProvider;
            this.StateValues = stateValues;
            this.State = state;
        }

        /// <summary>
        /// Gets the log timestamp.
        /// </summary>
        public DateTime Timestamp => this.Data.Timestamp;

        /// <summary>
        /// Gets the log <see cref="ActivityTraceId"/>.
        /// </summary>
        public ActivityTraceId TraceId => this.Data.TraceId;

        /// <summary>
        /// Gets the log <see cref="ActivitySpanId"/>.
        /// </summary>
        public ActivitySpanId SpanId => this.Data.SpanId;

        /// <summary>
        /// Gets the log <see cref="ActivityTraceFlags"/>.
        /// </summary>
        public ActivityTraceFlags TraceFlags => this.Data.TraceFlags;

        /// <summary>
        /// Gets the log trace state.
        /// </summary>
        public string? TraceState => this.Data.TraceState;

        /// <summary>
        /// Gets the log category name.
        /// </summary>
        public string? CategoryName => this.Data.CategoryName;

        /// <summary>
        /// Gets the log <see cref="Microsoft.Extensions.Logging.LogLevel"/>.
        /// </summary>
        public LogLevel LogLevel => this.Data.LogLevel;

        /// <summary>
        /// Gets the log <see cref="Microsoft.Extensions.Logging.EventId"/>.
        /// </summary>
        public EventId EventId => this.Data.EventId;

        /// <summary>
        /// Gets or sets the log formatted message.
        /// </summary>
        public string? FormattedMessage
        {
            get => this.Data.Message;
            set => this.Data.Message = value;
        }

        /// <summary>
        /// Gets or sets the raw state attached to the log. Set to <see
        /// langword="null"/> when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled.
        /// </summary>
        public object? State { get; set; }

        /// <summary>
        /// Gets or sets the parsed state values attached to the log. Set when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled
        /// otherwise <see langword="null"/>.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, object?>>? StateValues { get; set; }

        /// <summary>
        /// Gets the log <see cref="System.Exception"/>.
        /// </summary>
        public Exception? Exception => this.Data.Exception;

        internal IExternalScopeProvider? ScopeProvider { get; set; }

        /// <summary>
        /// Executes callback for each currently active scope objects in order
        /// of creation. All callbacks are guaranteed to be called inline from
        /// this method.
        /// </summary>
        /// <remarks>
        /// Note: Scopes are only available during the lifecycle of the log
        /// message being written. If you need to capture scopes to be used
        /// later (for example in batching scenarios), call <see
        /// cref="BufferLogScopes"/> to safely capture the values (incurs
        /// allocation).
        /// </remarks>
        /// <typeparam name="TState">State.</typeparam>
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
            else if (this.ScopeProvider != null)
            {
                this.ScopeProvider.ForEachScope(ScopeForEachState<TState>.ForEachScope, forEachScopeState);
            }
        }

        /// <summary>
        /// Gets a reference to the <see cref="LogRecordData"/> for the log message.
        /// </summary>
        /// <returns><see cref="LogRecordData"/>.</returns>
        public ref LogRecordData GetDataRef()
        {
            return ref this.Data;
        }

        /// <summary>
        /// Buffers the scopes attached to the log into a list so that they can
        /// be safely processed after the log message lifecycle has ended.
        /// </summary>
        internal void BufferLogScopes()
        {
            if (this.ScopeProvider == null || this.BufferedScopes != null)
            {
                return;
            }

            List<object?> scopes = new List<object?>();

            this.ScopeProvider?.ForEachScope(AddScopeToBufferedList, scopes);

            this.BufferedScopes = scopes;
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
