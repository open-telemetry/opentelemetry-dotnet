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
using Microsoft.Extensions.Options;

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
        private readonly EventLevel? defaultMinEventLevel;
        private readonly List<EventSource> eventSources = new List<EventSource>();
        private readonly ILoggerFactory loggerFactory;
        private readonly IOptionsMonitor<LoggerFilterOptions> loggerFilterOptions;
        private readonly ConcurrentDictionary<EventSource, ILogger> loggers = new ConcurrentDictionary<EventSource, ILogger>();

        private readonly Func<EventSourceEvent, Exception, string> formatMessage = FormatMessage;

        internal SelfDiagnosticsEventLogForwarder(ILoggerFactory loggerFactory, IOptionsMonitor<LoggerFilterOptions> loggerFilterOptions, EventLevel? defaultMinEventLevel = null)
        {
            this.loggerFactory = loggerFactory;
            this.loggerFilterOptions = loggerFilterOptions;
            this.defaultMinEventLevel = defaultMinEventLevel;

            // set initial levels on existing event sources
            this.SetEventSourceLevels();

            // listen to changes to the log levels
            loggerFilterOptions.OnChange(o => this.SetEventSourceLevels());
        }

        public override void Dispose()
        {
            this.StopForwarding();
            base.Dispose();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
            {
                lock (this.lockObj)
                {
                    this.eventSources.Add(eventSource);
                }

                this.SetEventSourceLevel(eventSource);
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

            if (!this.loggers.TryGetValue(eventData.EventSource, out var logger))
            {
                logger = this.loggers.GetOrAdd(eventData.EventSource, eventSource => this.loggerFactory.CreateLogger(ToLoggerName(eventSource.Name)));
            }

            logger.Log(MapLevel(eventData.Level), new EventId(eventData.EventId, eventData.EventName), new EventSourceEvent(eventData), null, this.formatMessage);
        }

        private static string ToLoggerName(string name)
        {
            return name.Replace('-', '.');
        }

        private static LogLevel MapLevel(EventLevel level)
        {
            return level switch
            {
                EventLevel.Critical => LogLevel.Critical,
                EventLevel.Error => LogLevel.Error,
                EventLevel.Informational => LogLevel.Information,
                EventLevel.Verbose => LogLevel.Debug,
                EventLevel.Warning => LogLevel.Warning,
                EventLevel.LogAlways => LogLevel.Information,
                _ => LogLevel.None,
            };
        }

        private static EventLevel? MapLevel(LogLevel? level)
        {
            if (!level.HasValue)
            {
                return null;
            }

            return level switch
            {
                LogLevel.Critical => EventLevel.Critical,
                LogLevel.Error => EventLevel.Error,
                LogLevel.Information => EventLevel.Informational,
                LogLevel.Debug => EventLevel.Verbose,
                LogLevel.Trace => EventLevel.LogAlways,
                LogLevel.Warning => EventLevel.Warning,
                _ => null,
            };
        }

        private static string FormatMessage(EventSourceEvent eventSourceEvent, Exception exception)
        {
            return EventSourceEventFormatter.Format(eventSourceEvent.EventData);
        }

        private EventLevel? GetEventLevel(string category)
        {
            var options = this.loggerFilterOptions?.CurrentValue;
            if (options == null)
            {
                return this.defaultMinEventLevel;
            }

            EventLevel? minLevel = MapLevel(options.MinLevel);

            LoggerFilterRule current = null;
            foreach (LoggerFilterRule rule in options.Rules)
            {
                if (this.IsBetterRule(rule, current, category))
                {
                    current = rule;
                }
            }

            if (current != null)
            {
                minLevel = MapLevel(current.LogLevel);
            }

            return minLevel;
        }

        private bool IsBetterRule(LoggerFilterRule rule, LoggerFilterRule current, string category)
        {
            string categoryName = rule.CategoryName;
            if (categoryName != null)
            {
                const char WildcardChar = '*';

                int wildcardIndex = categoryName.IndexOf(WildcardChar);
                if (wildcardIndex != -1 &&
                    categoryName.IndexOf(WildcardChar, wildcardIndex + 1) != -1)
                {
                    // can't have more than one wildcard
                    return false;
                }

                ReadOnlySpan<char> prefix, suffix;
                if (wildcardIndex == -1)
                {
                    prefix = categoryName.AsSpan();
                    suffix = default;
                }
                else
                {
                    prefix = categoryName.AsSpan(0, wildcardIndex);
                    suffix = categoryName.AsSpan(wildcardIndex + 1);
                }

                if (!category.AsSpan().StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !category.AsSpan().EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (current?.CategoryName != null)
            {
                if (rule.CategoryName == null)
                {
                    return false;
                }

                if (current.CategoryName.Length > rule.CategoryName.Length)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetEventSourceLevels()
        {
            lock (this.lockObj)
            {
                foreach (var eventSource in this.eventSources)
                {
                    this.SetEventSourceLevel(eventSource);
                }
            }
        }

        private void StopForwarding()
        {
            lock (this.lockObj)
            {
                foreach (var eventSource in this.eventSources)
                {
                    this.DisableEvents(eventSource);
                }
            }
        }

        private void SetEventSourceLevel(EventSource eventSource)
        {
            var eventLevel = this.GetEventLevel(ToLoggerName(eventSource.Name));

            if (eventLevel.HasValue)
            {
                this.EnableEvents(eventSource, eventLevel.Value);
            }
            else
            {
                this.DisableEvents(eventSource);
            }
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
