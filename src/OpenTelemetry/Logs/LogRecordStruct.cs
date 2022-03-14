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

        public DateTime Timestamp { get; }

        public string CategoryName { get; }

        public LogLevel LogLevel { get; }

        public EventId EventId { get; }

        public string FormattedMessage { get; }

        public Exception Exception { get; }

        public void ForEachScope<TState>(Action<LogRecordScope, TState> callback, TState state)
        {
            if (this.scopeProvider != null)
            {
                var forEachScopeState = new LogRecord.ScopeForEachState<TState>(callback, state);

                this.scopeProvider.ForEachScope(LogRecord.ScopeForEachState<TState>.ForEachScope, forEachScopeState);
            }
        }

        public void ForEachStateValue<TState>(Action<KeyValuePair<string, object>, TState> callback, TState state)
        {
            if (this.stateValues != null)
            {
                for (int i = 0; i < this.stateValues.Count; i++)
                {
                    callback(this.stateValues[i], state);
                }
            }
            else if (this.state != null)
            {
                callback(new KeyValuePair<string, object>(string.Empty, this.state), state);
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
