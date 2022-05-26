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
        private static readonly Action<object?, List<object?>> AddScopeToBufferedList = (object? scope, List<object?> state) =>
        {
            state.Add(scope);
        };

        private List<object?>? bufferedScopes;
        private DateTime timestamp;

        internal LogRecord()
        {
            this.timestamp = DateTime.UtcNow;
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
            this.timestamp = timestamp;

            this.ScopeProvider = scopeProvider;
            this.CategoryName = categoryName;
            this.LogLevel = logLevel;
            this.EventId = eventId;
            this.FormattedMessage = formattedMessage;
            this.State = state;
            this.StateValues = stateValues;
            this.Exception = exception;

            this.SetActivityContext(Activity.Current);
        }

        /// <summary>
        /// Gets or sets the log timestamp.
        /// </summary>
        public DateTime Timestamp
        {
            get => this.timestamp;
            set { this.timestamp = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value; }
        }

        /// <summary>
        /// Gets or sets the log <see cref="ActivityTraceId"/>.
        /// </summary>
        public ActivityTraceId TraceId { get; set; }

        /// <summary>
        /// Gets or sets the log <see cref="ActivitySpanId"/>.
        /// </summary>
        public ActivitySpanId SpanId { get; set; }

        /// <summary>
        /// Gets or sets the log <see cref="ActivityTraceFlags"/>.
        /// </summary>
        public ActivityTraceFlags TraceFlags { get; set; }

        /// <summary>
        /// Gets or sets the log trace state.
        /// </summary>
        public string? TraceState { get; set; }

        /// <summary>
        /// Gets or sets the log category name.
        /// </summary>
        public string? CategoryName { get; set; }

        /// <summary>
        /// Gets or sets the log <see cref="Microsoft.Extensions.Logging.LogLevel"/>.
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the log <see cref="Microsoft.Extensions.Logging.EventId"/>.
        /// </summary>
        public EventId EventId { get; set; }

        /// <summary>
        /// Gets or sets the log formatted message.
        /// </summary>
        public string? FormattedMessage { get; set; }

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
        /// Gets or sets the log <see cref="System.Exception"/>.
        /// </summary>
        public Exception? Exception { get; set; }

        internal IExternalScopeProvider? ScopeProvider { get; set; }

        /// <summary>
        /// Sets the log <see cref="TraceId"/>, <see cref="SpanId"/>, <see
        /// cref="TraceState"/>, and <see cref="TraceFlags"/> from the supplied
        /// <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/>.</param>
        public void SetActivityContext(Activity? activity)
        {
            if (activity != null)
            {
                this.TraceId = activity.TraceId;
                this.SpanId = activity.SpanId;
                this.TraceState = activity.TraceStateString;
                this.TraceFlags = activity.ActivityTraceFlags;
            }
        }

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

            if (this.bufferedScopes != null)
            {
                foreach (object? scope in this.bufferedScopes)
                {
                    ScopeForEachState<TState>.ForEachScope(scope, forEachScopeState);
                }
            }
            else if (this.ScopeProvider != null)
            {
                this.ScopeProvider.ForEachScope(ScopeForEachState<TState>.ForEachScope, forEachScopeState);
            }
        }

        internal void Clear(bool clearAllData)
        {
            this.timestamp = DateTime.UtcNow;

            if (!clearAllData)
            {
                return;
            }

            this.CategoryName = null;
            this.LogLevel = LogLevel.Trace;
            this.EventId = default;
            this.FormattedMessage = null;
            this.State = null;
            this.StateValues = null;
            this.Exception = null;

            this.TraceId = default;
            this.SpanId = default;
            this.TraceState = null;
            this.TraceFlags = ActivityTraceFlags.None;
        }

        /// <summary>
        /// Buffers the scopes attached to the log into a list so that they can
        /// be safely processed after the log message lifecycle has ended.
        /// </summary>
        internal void BufferLogScopes()
        {
            if (this.ScopeProvider == null || this.bufferedScopes != null)
            {
                return;
            }

            List<object?> scopes = new List<object?>();

            this.ScopeProvider?.ForEachScope(AddScopeToBufferedList, scopes);

            this.bufferedScopes = scopes;
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
