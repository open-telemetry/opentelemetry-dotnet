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
        private static readonly Action<object, List<object>> AddScopeToBufferedList = (object scope, List<object> state) =>
        {
            state.Add(scope);
        };

        private SpinLock _spinlock = default;

        private bool _lockTaken = false;

        private List<KeyValuePair<string, object>> _stateList;

        private List<KeyValuePair<string, object>> _stateValues;

        private string _formattedMessage;

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

            if (formattedMessage != null)
            {
                this.FormattedMessage = formattedMessage;
            }

            if (state != null)
            {
                //var parsedState = state;
                //this._stateList = parsedState;
            }

            if (stateValues != null)
            {
                this.StateValues = stateValues;
            }

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

        /// <summary>
        /// Gets or sets the FormattedMessage of the log.
        /// </summary>
        public string FormattedMessage
        {
            get => this._formattedMessage;
            set
            {
                Guard.ThrowIfNull(value, nameof(this.FormattedMessage));
                try
                {
                    Guard.ThrowIfNullOrEmpty(value, nameof(this.FormattedMessage));
                    this._formattedMessage = value;
                } finally
                {
                    if (_lockTaken)
                    {
                        _spinlock.Exit();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the raw state attached to the log. Set to <see
        /// langword="null"/> when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled.
        /// </summary>
        public object State
        {
            get => this._stateList;
            set
            {
                Guard.ThrowIfNull(value, nameof(this.State));
                try
                {
                    _spinlock.Enter(ref _lockTaken);
                    var listKvp = value as IReadOnlyList<KeyValuePair<string, object>>;
                    int kvpCount = listKvp.Count;
                    var tempList = new List<KeyValuePair<string, object>>(kvpCount);
                    for (int i = 0; i < kvpCount; ++i)
                    {
                        var updatedEntry = new KeyValuePair<string, object>(listKvp[i].Key, listKvp[i].Value);
                        tempList.Add(updatedEntry);
                    }

                    this._stateList = tempList;
                }
                finally
                {
                    if (_lockTaken)
                    {
                        _spinlock.Exit();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the parsed state values attached to the log. Set when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is enabled
        /// otherwise <see langword="null"/>.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, object>> StateValues
        {
            get => (IReadOnlyList<KeyValuePair<string, object>>)this._stateValues;
            set
            {
                Guard.ThrowIfNull(value, nameof(this.StateValues));
                try
                {
                    _spinlock.Enter(ref _lockTaken);
                    int kvpCount = value.Count;
                    var tempStateValues = new List<KeyValuePair<string, object>>(kvpCount);
                    for (int i = 0; i < kvpCount; ++i)
                    {
                        var updatedEntry = new KeyValuePair<string, object>(value[i].Key, value[i].Value);
                        tempStateValues.Add(updatedEntry);
                    }

                    this._stateValues = tempStateValues;
                }
                finally
                {
                    if (_lockTaken)
                    {
                        _spinlock.Exit();
                    }
                }
            }
        }

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
        public void ForEachScope<TState>(Action<LogRecordScope, TState> callback, TState state)
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

            List<object> scopes = new List<object>();

            this.ScopeProvider?.ForEachScope(AddScopeToBufferedList, scopes);

            this.bufferedScopes = scopes;
        }

        private readonly struct ScopeForEachState<TState>
        {
            public static readonly Action<object, ScopeForEachState<TState>> ForEachScope = (object scope, ScopeForEachState<TState> state) =>
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
