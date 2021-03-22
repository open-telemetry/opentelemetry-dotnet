// <copyright file="SelfDiagnosticsEventLogForwarder.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// SelfDiagnosticsEventListener class enables the events from OpenTelemetry event sources
    /// and write the events to an ILoggerFactory.
    /// </summary>
    internal class SelfDiagnosticsEventLogForwarder : EventListener
    {
        private const string EventSourceNamePrefix = "OpenTelemetry-";
        private readonly object lockObj = new object();
        private readonly EventLevel logLevel;
        private readonly List<EventSource> eventSourcesBeforeConstructor = new List<EventSource>();
        private readonly ILoggerFactory loggerFactory;
        private readonly ConcurrentDictionary<string, ILogger> loggers = new ConcurrentDictionary<string, ILogger>();

        private readonly Func<EventSourceEvent, Exception, string> formatMessage = FormatMessage;

        internal SelfDiagnosticsEventLogForwarder(EventLevel logLevel, ILoggerFactory loggerFactory)
        {
            this.logLevel = logLevel;
            this.loggerFactory = loggerFactory;

            List<EventSource> eventSources;
            lock (this.lockObj)
            {
                eventSources = this.eventSourcesBeforeConstructor;
                this.eventSourcesBeforeConstructor = null;
            }

            foreach (var eventSource in eventSources)
            {
                this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
            {
                // If there are EventSource classes already initialized as of now, this method would be called from
                // the base class constructor before the first line of code in SelfDiagnosticsEventListener constructor.
                // In this case logLevel is always its default value, "LogAlways".
                // Thus we should save the event source and enable them later, when code runs in constructor.
                if (this.eventSourcesBeforeConstructor != null)
                {
                    lock (this.lockObj)
                    {
                        if (this.eventSourcesBeforeConstructor != null)
                        {
                            this.eventSourcesBeforeConstructor.Add(eventSource);
                            return;
                        }
                    }
                }

                this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
            }

            base.OnEventSourceCreated(eventSource);
        }

        /// <summary>
        /// This method records the events from event sources to the logging system.
        /// </summary>
        /// <param name="eventData">Data of the EventSource event.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (this.loggerFactory == null)
            {
                return;
            }

            var logger = this.loggers.GetOrAdd(eventData.EventSource.Name, name => this.loggerFactory.CreateLogger(ToLoggerName(name)));
            logger.Log(MapLevel(eventData.Level), new EventId(eventData.EventId, eventData.EventName), new EventSourceEvent(eventData), null, this.formatMessage);
        }

        private static string ToLoggerName(string name)
        {
            return name.Replace('-', '.');
        }

        private static LogLevel MapLevel(EventLevel level)
        {
            switch (level)
            {
                case EventLevel.Critical:
                    return LogLevel.Critical;
                case EventLevel.Error:
                    return LogLevel.Error;
                case EventLevel.Informational:
                    return LogLevel.Information;
                case EventLevel.Verbose:
                    return LogLevel.Debug;
                case EventLevel.Warning:
                    return LogLevel.Warning;
                case EventLevel.LogAlways:
                    return LogLevel.Information;
                default:
                    return LogLevel.None;
            }
        }

        private static string FormatMessage(EventSourceEvent eventSourceEvent, Exception exception)
        {
            return EventSourceEventFormatter.Format(eventSourceEvent.EventData);
        }

        private readonly struct EventSourceEvent : IReadOnlyList<KeyValuePair<string, object>>
        {
            public EventSourceEvent(EventWrittenEventArgs eventData)
            {
                this.EventData = eventData;
            }

            public EventWrittenEventArgs EventData { get; }

            public int Count => this.EventData.PayloadNames.Count;

            public KeyValuePair<string, object> this[int index] => new KeyValuePair<string, object>(this.EventData.PayloadNames[index], this.EventData.Payload[index]);

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                for (int i = 0; i < this.Count; i++)
                {
                    yield return new KeyValuePair<string, object>(this.EventData.PayloadNames[i], this.EventData.Payload[i]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
