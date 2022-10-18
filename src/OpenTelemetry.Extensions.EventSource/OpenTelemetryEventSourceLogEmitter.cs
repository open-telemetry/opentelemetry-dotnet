// <copyright file="OpenTelemetryEventSourceLogEmitter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Implements an <see cref="EventListener"/> which will convert <see
    /// cref="EventSource"/> events into OpenTelemetry logs.
    /// </summary>
    public sealed class OpenTelemetryEventSourceLogEmitter : EventListener
    {
        private static readonly Tuple<LogRecordSeverity, string>[] EventLevels = new Tuple<LogRecordSeverity, string>[]
        {
            new((LogRecordSeverity)(-1), nameof(EventLevel.LogAlways)),
            new(LogRecordSeverity.Fatal, nameof(EventLevel.Critical)),
            new(LogRecordSeverity.Error, nameof(EventLevel.Error)),
            new(LogRecordSeverity.Warning, nameof(EventLevel.Warning)),
            new(LogRecordSeverity.Information, nameof(EventLevel.Informational)),
            new(LogRecordSeverity.Trace, nameof(EventLevel.Verbose)),
        };

        private readonly bool includeFormattedMessage;
        private readonly LoggerProvider loggerProvider;
        private readonly object lockObj = new();
        private readonly Func<string, EventLevel?> shouldListenToFunc;
        private readonly List<EventSource> eventSources = new();
        private readonly List<EventSource>? eventSourcesBeforeConstructor = new();
        private readonly bool disposeProvider;
        private readonly Logger logger;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="OpenTelemetryEventSourceLogEmitter"/> class.
        /// </summary>
        /// <param name="loggerProvider"><see
        /// cref="LoggerProvider"/>.</param>
        /// <param name="shouldListenToFunc">Callback function used to decide if
        /// events should be captured for a given <see
        /// cref="EventSource.Name"/>. Return <see langword="null"/> if no
        /// events should be captured.</param>
        /// <param name="options"><see cref="OpenTelemetryEventSourceLogEmitterOptions"/>.</param>
        /// <param name="disposeProvider">Controls whether or not the supplied
        /// <paramref name="loggerProvider"/> will be disposed when
        /// the <see cref="EventListener"/> is disposed. Default value: <see
        /// langword="false"/>.</param>
        public OpenTelemetryEventSourceLogEmitter(
            LoggerProvider loggerProvider,
            Func<string, EventLevel?> shouldListenToFunc,
            OpenTelemetryEventSourceLogEmitterOptions? options = null,
            bool disposeProvider = false)
        {
            Guard.ThrowIfNull(loggerProvider);
            Guard.ThrowIfNull(shouldListenToFunc);

            options ??= new();

            this.includeFormattedMessage = options.IncludeFormattedMessage;
            this.loggerProvider = loggerProvider!;
            this.disposeProvider = disposeProvider;
            this.shouldListenToFunc = shouldListenToFunc;

            this.logger = loggerProvider.GetLogger(new LoggerOptions(
                new InstrumentationScope("OpenTelemetry.Extensions.EventSource")
                {
                    Version = $"semver:{typeof(OpenTelemetryEventSourceLogEmitter).Assembly.GetName().Version}",
                }));

            lock (this.lockObj)
            {
                foreach (EventSource eventSource in this.eventSourcesBeforeConstructor)
                {
                    this.ProcessSource(eventSource);
                }

                this.eventSourcesBeforeConstructor = null;
            }
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            foreach (EventSource eventSource in this.eventSources)
            {
                this.DisableEvents(eventSource);
            }

            this.eventSources.Clear();

            if (this.disposeProvider)
            {
                this.loggerProvider.Dispose();
            }

            base.Dispose();
        }

#pragma warning disable CA1062 // Validate arguments of public methods
        /// <inheritdoc/>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            Debug.Assert(eventSource != null, "EventSource was null.");

            try
            {
                if (this.eventSourcesBeforeConstructor != null)
                {
                    lock (this.lockObj)
                    {
                        if (this.eventSourcesBeforeConstructor != null)
                        {
                            this.eventSourcesBeforeConstructor.Add(eventSource!);
                            return;
                        }
                    }
                }

                this.ProcessSource(eventSource!);
            }
            finally
            {
                base.OnEventSourceCreated(eventSource);
            }
        }
#pragma warning restore CA1062 // Validate arguments of public methods

#pragma warning disable CA1062 // Validate arguments of public methods
        /// <inheritdoc/>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Debug.Assert(eventData != null, "EventData was null.");

            string? rawMessage = eventData!.Message;

            LogRecordData data = new(Activity.Current)
            {
#if !NETFRAMEWORK
                Timestamp = eventData.TimeStamp,
#endif
            };

            uint eventLevel = (uint)eventData.Level;
            if (eventLevel < 6)
            {
                Tuple<LogRecordSeverity, string> eventLevelMapping = EventLevels[eventLevel];
                data.Severity = eventLevelMapping.Item1;
                data.SeverityText = eventLevelMapping.Item2;
            }

            LogRecordAttributeList attributes = default;

            attributes.Add("event_source.name", eventData.EventSource.Name);
            attributes.Add("event_source.event_id", eventData.EventId);
            attributes.Add("event_source.event_name", eventData.EventName);

            if (eventData.ActivityId != Guid.Empty)
            {
                attributes.Add("event_source.activity_id", eventData.ActivityId);
            }

            if (eventData.RelatedActivityId != Guid.Empty)
            {
                attributes.Add("event_source.related_activity_id", eventData.RelatedActivityId);
            }

            int payloadCount = eventData.Payload?.Count ?? 0;

            if (payloadCount > 0 && payloadCount == eventData.PayloadNames?.Count)
            {
                for (int i = 0; i < payloadCount; i++)
                {
                    string name = eventData.PayloadNames[i];

                    if (!string.IsNullOrEmpty(rawMessage))
                    {
                        // TODO: This code converts the event message from
                        // string.Format syntax (eg: "Some message {0} {1}")
                        // into structured log format (eg: "Some message
                        // {propertyName1} {propertyName2}") but it is
                        // expensive. Probably needs a cache.
#if NETFRAMEWORK
                        rawMessage = rawMessage.Replace($"{{{i}}}", $"{{{name}}}");
#else
                        rawMessage = rawMessage.Replace($"{{{i}}}", $"{{{name}}}", StringComparison.Ordinal);
#endif
                    }

                    attributes.Add(name, eventData.Payload![i]);
                }
            }

            if (!string.IsNullOrEmpty(rawMessage))
            {
                data.Body = rawMessage;

                if (this.includeFormattedMessage && payloadCount > 0)
                {
                    attributes.Add("event_source.formatted_message", string.Format(CultureInfo.InvariantCulture, eventData.Message!, eventData.Payload!.ToArray()));
                }
            }

            this.logger.EmitLog(in data, in attributes);
        }
#pragma warning restore CA1062 // Validate arguments of public methods

        private void ProcessSource(EventSource eventSource)
        {
            EventLevel? eventLevel = this.shouldListenToFunc(eventSource.Name);

            if (eventLevel.HasValue)
            {
                this.eventSources.Add(eventSource);
                this.EnableEvents(eventSource, eventLevel.Value, EventKeywords.All);
            }
        }
    }
}
