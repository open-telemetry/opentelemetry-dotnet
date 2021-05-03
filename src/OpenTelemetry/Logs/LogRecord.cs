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

#if NET461 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Stores details about a log message.
    /// </summary>
    public sealed class LogRecord
    {
        private List<object> bufferedScopes;

        internal LogRecord(
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

            var activity = Activity.Current;
            if (activity != null)
            {
                this.TraceId = activity.TraceId;
                this.SpanId = activity.SpanId;
                this.TraceState = activity.TraceStateString;
                this.TraceFlags = activity.ActivityTraceFlags;
            }

            this.Timestamp = timestamp;
            this.CategoryName = categoryName;
            this.LogLevel = logLevel;
            this.EventId = eventId;
            this.FormattedMessage = formattedMessage;
            this.State = state;
            this.StateValues = stateValues;
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
        public IReadOnlyList<KeyValuePair<string, object>> StateValues { get; }

        public Exception Exception { get; }

        internal IExternalScopeProvider ScopeProvider { get; set; }

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
        public unsafe void ForEachScope<TState>(LogRecordScopeCallback<TState> callback, TState state)
        {
            if (this.bufferedScopes != null)
            {
                ScopeForEachState<TState> forEachState = new ScopeForEachState<TState>(callback, state, 0);
                IntPtr statePointer = new IntPtr(Unsafe.AsPointer(ref forEachState));

                foreach (object scope in this.bufferedScopes)
                {
                    ScopeForEachState<TState>.ForEachScope(scope, statePointer);
                }
            }
            else if (this.ScopeProvider != null)
            {
                ScopeForEachState<TState> forEachState = new ScopeForEachState<TState>(callback, state, 0);
                IntPtr statePointer = new IntPtr(Unsafe.AsPointer(ref forEachState));

                this.ScopeProvider.ForEachScope(ScopeForEachState<TState>.ForEachScope, statePointer);
            }
        }

        /// <summary>
        /// Buffers the scopes attached to the log into a list so that they can
        /// be safely processed after the log message lifecycle has ended.
        /// </summary>
        public void BufferLogScopes()
        {
            if (this.ScopeProvider == null || this.bufferedScopes != null)
            {
                return;
            }

            List<object> scopes = new List<object>();

            this.ScopeProvider?.ForEachScope(AddScopeToList, scopes);

            this.bufferedScopes = scopes;

            static void AddScopeToList(object scope, List<object> state)
            {
                state.Add(scope);
            }
        }

        private unsafe struct ScopeForEachState<TState>
        {
            public static readonly Action<object, IntPtr> ForEachScope = (object scope, IntPtr statePointer) =>
            {
                ref ScopeForEachState<TState> state = ref Unsafe.AsRef<ScopeForEachState<TState>>((void*)statePointer);

                LogRecordScope logRecordScope = new LogRecordScope(scope, state.Depth++);

                state.Callback(logRecordScope, state.UserState);
            };

            public readonly LogRecordScopeCallback<TState> Callback;

            public readonly TState UserState;

            public int Depth;

            public ScopeForEachState(LogRecordScopeCallback<TState> callback, TState state, int depth)
            {
                this.Callback = callback;
                this.UserState = state;
                this.Depth = depth;
            }
        }
    }
}
#endif
