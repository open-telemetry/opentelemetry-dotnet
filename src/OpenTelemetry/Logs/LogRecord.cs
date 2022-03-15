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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Stores details about a log message.
    /// </summary>
    public sealed class LogRecord
    {
        private static readonly Action<object, List<object>> AddScopeToBufferedList = (object scope, List<object> state) =>
        {
            state.Add(scope);
        };

        private readonly IReadOnlyList<KeyValuePair<string, object>> stateValues;
        private List<object> bufferedScopes;
        private IReadOnlyList<KeyValuePair<string, object>> bufferedStateValues;

        internal LogRecord(
            in ActivityContext activityContext,
            IExternalScopeProvider scopeProvider,
            DateTime timestamp,
            string categoryName,
            LogLevel logLevel,
            EventId eventId,
            string formattedMessage,
            object state,
            Exception exception,
            IReadOnlyList<KeyValuePair<string, object>> stateValues)
        {
            this.ScopeProvider = scopeProvider;

            if (activityContext.IsValid())
            {
                this.TraceId = activityContext.TraceId;
                this.SpanId = activityContext.SpanId;
                this.TraceState = activityContext.TraceState;
                this.TraceFlags = activityContext.TraceFlags;
            }

            this.Timestamp = timestamp;
            this.CategoryName = categoryName;
            this.LogLevel = logLevel;
            this.EventId = eventId;
            this.FormattedMessage = formattedMessage;
            this.State = state;
            this.stateValues = stateValues;
            this.Exception = exception;
        }

        public DateTime Timestamp { get; }

        public ActivityTraceId TraceId { get; }

        public ActivitySpanId SpanId { get; }

        public ActivityTraceFlags TraceFlags { get; }

        public string TraceState { get; }

        public string CategoryName { get; }

        public LogLevel LogLevel { get; }

        public EventId EventId { get; }

        public string FormattedMessage { get; }

        /// <summary>
        /// Gets the raw state attached to the log. Set to <see
        /// langword="null"/> when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled.
        /// </summary>
        public object State { get; }

        /// <summary>
        /// Gets the parsed state values attached to the log. Set when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled
        /// otherwise <see langword="null"/>.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, object>> StateValues => this.bufferedStateValues ?? this.stateValues;

        public Exception Exception { get; }

        internal IExternalScopeProvider ScopeProvider { get; set; }

        /// <summary>
        /// Executes callback for each currently active scope objects in order
        /// of creation. All callbacks are guaranteed to be called inline from
        /// this method.
        /// </summary>
        /// <typeparam name="TState">State.</typeparam>
        /// <param name="callback">The callback to be executed for every scope object.</param>
        /// <param name="state">The state object to be passed into the callback.</param>
        public void ForEachScope<TState>(LogRecordScopeCallback<TState> callback, TState state)
        {
            var forEachScopeState = new ScopeForEachState<TState>(callback, state);

            if (this.bufferedScopes != null)
            {
                foreach (object scope in this.bufferedScopes)
                {
                    ScopeForEachState<TState>.ForEachScope(scope, forEachScopeState);
                }
            }
            else if (this.ScopeProvider != null)
            {
                this.ScopeProvider.ForEachScope(ScopeForEachState<TState>.ForEachScope, forEachScopeState);
            }
        }

        internal void Buffer()
        {
            this.BufferLogState();
            this.BufferLogScopes();
        }

        /// <summary>
        /// Buffers the state attached to the log into a list so that it can be
        /// safely processed after the log message lifecycle has ended.
        /// </summary>
        private void BufferLogState()
        {
            if (this.bufferedStateValues != null || this.stateValues == null)
            {
                return;
            }

            List<KeyValuePair<string, object>> bufferedStateValues = new(this.stateValues.Count);

            for (int i = 0; i < this.stateValues.Count; i++)
            {
                bufferedStateValues.Add(this.stateValues[i]);
            }

            this.bufferedStateValues = bufferedStateValues;
        }

        /// <summary>
        /// Buffers the scopes attached to the log into a list so that they can
        /// be safely processed after the log message lifecycle has ended.
        /// </summary>
        private void BufferLogScopes()
        {
            if (this.ScopeProvider == null || this.bufferedScopes != null)
            {
                return;
            }

            List<object> scopes = new List<object>();

            this.ScopeProvider?.ForEachScope(AddScopeToBufferedList, scopes);

            this.bufferedScopes = scopes;
        }

        internal readonly struct ScopeForEachState<TState>
        {
            public static readonly Action<object, ScopeForEachState<TState>> ForEachScope = (object scope, ScopeForEachState<TState> state) =>
            {
                LogRecordScope logRecordScope = new LogRecordScope(scope);

                state.Callback(logRecordScope, state.UserState);
            };

            public readonly LogRecordScopeCallback<TState> Callback;

            public readonly TState UserState;

            public ScopeForEachState(LogRecordScopeCallback<TState> callback, TState state)
            {
                this.Callback = callback;
                this.UserState = state;
            }
        }
    }
}
