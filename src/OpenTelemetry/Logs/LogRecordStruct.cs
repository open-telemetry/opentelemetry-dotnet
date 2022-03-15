// <copyright file="LogRecordStruct.cs" company="OpenTelemetry Authors">
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
    public readonly ref struct LogRecordStruct
    {
        private readonly IExternalScopeProvider scopeProvider;
        private readonly object state;
        private readonly IReadOnlyList<KeyValuePair<string, object>> stateValues;

        internal LogRecordStruct(
            DateTime timestamp,
            string categoryName,
            LogLevel logLevel,
            EventId eventId,
            string formattedMessage,
            Exception exception,
            IExternalScopeProvider scopeProvider,
            object state,
            IReadOnlyList<KeyValuePair<string, object>> stateValues)
        {
            this.Timestamp = timestamp;
            this.CategoryName = categoryName;
            this.LogLevel = logLevel;
            this.EventId = eventId;
            this.FormattedMessage = formattedMessage;
            this.Exception = exception;
            this.scopeProvider = scopeProvider;
            this.state = state;
            this.stateValues = stateValues;
        }

        /// <summary>
        /// Gets the timestamp for the log message.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the category name for the log message.
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// Gets the <see cref="Microsoft.Extensions.Logging.LogLevel"/> for the log message.
        /// </summary>
        public LogLevel LogLevel { get; }

        /// <summary>
        /// Gets the <see cref="Microsoft.Extensions.Logging.EventId"/> for the log message.
        /// </summary>
        public EventId EventId { get; }

        /// <summary>
        /// Gets the formatted message for the log. Only available when <see
        /// cref="OpenTelemetryLoggerOptions.IncludeFormattedMessage"/> is <see
        /// langword="true"/>.
        /// </summary>
        public string FormattedMessage { get; }

        /// <summary>
        /// Gets the <see cref="System.Exception"/> for the log message.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the state for the log message.
        /// </summary>
        public LogRecordState State => new(this.state, this.stateValues);

        /// <summary>
        /// Executes callback for each currently active scope objects in order
        /// of creation. All callbacks are guaranteed to be called inline from
        /// this method. Only available when <see
        /// cref="OpenTelemetryLoggerOptions.IncludeScopes"/> is <see
        /// langword="true"/>.
        /// </summary>
        /// <typeparam name="TState">State.</typeparam>
        /// <param name="callback">The callback to be executed for every scope object.</param>
        /// <param name="state">The state object to be passed into the callback.</param>
        public void ForEachScope<TState>(LogRecordScopeCallback<TState> callback, TState state)
        {
            if (this.scopeProvider != null)
            {
                var forEachScopeState = new LogRecord.ScopeForEachState<TState>(callback, state);

                this.scopeProvider.ForEachScope(LogRecord.ScopeForEachState<TState>.ForEachScope, forEachScopeState);
            }
        }

        internal static LogRecord ToLogRecord(in LogRecordStruct logRecord)
        {
            var activityContext = Activity.Current?.Context ?? default;

            return new LogRecord(
                in activityContext,
                logRecord.scopeProvider,
                logRecord.Timestamp,
                logRecord.CategoryName,
                logRecord.LogLevel,
                logRecord.EventId,
                logRecord.FormattedMessage,
                logRecord.state,
                logRecord.Exception,
                logRecord.stateValues);
        }
    }
}
