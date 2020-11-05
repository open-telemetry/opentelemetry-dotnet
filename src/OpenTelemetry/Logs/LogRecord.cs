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
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Log record base class.
    /// </summary>
    public sealed class LogRecord
    {
        internal LogRecord(DateTime timestamp, string categoryName, LogLevel logLevel, EventId eventId, object state, Exception exception)
        {
            this.Timestamp = timestamp;

            var activity = Activity.Current;
            if (activity != null)
            {
                this.TraceId = activity.TraceId;
                this.SpanId = activity.SpanId;
                this.TraceState = activity.TraceStateString;
                this.TraceFlags = activity.ActivityTraceFlags;
            }

            this.CategoryName = categoryName;
            this.LogLevel = logLevel;
            this.EventId = eventId;
            this.State = state;
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

        public object State { get; }

        public Exception Exception { get; }
    }
}
#endif
