// <copyright file="OpenTelemetrySdkEventSource.cs" company="OpenTelemetry Authors">
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
#if DEBUG
using System.Collections.Generic;
using System.Linq;
#endif
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Security;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// EventSource implementation for OpenTelemetry SDK implementation.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Sdk")]
    internal class OpenTelemetrySdkEventSource : EventSource
    {
        public static OpenTelemetrySdkEventSource Log = new();
#if DEBUG
        public static OpenTelemetryEventListener Listener = new();
#endif

        [NonEvent]
        public void SpanProcessorException(string evnt, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.SpanProcessorException(evnt, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void TracestateExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.TracestateExtractError(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void MetricObserverCallbackException(Exception exception)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                if (exception is AggregateException aggregateException)
                {
                    foreach (var ex in aggregateException.InnerExceptions)
                    {
                        this.ObservableInstrumentCallbackException(ex.ToInvariantString());
                    }
                }
                else
                {
                    this.ObservableInstrumentCallbackException(exception.ToInvariantString());
                }
            }
        }

        [NonEvent]
        public void MetricReaderException(string methodName, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.MetricReaderException(methodName, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void TracestateKeyIsInvalid(ReadOnlySpan<char> key)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.TracestateKeyIsInvalid(key.ToString());
            }
        }

        [NonEvent]
        public void TracestateValueIsInvalid(ReadOnlySpan<char> value)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.TracestateValueIsInvalid(value.ToString());
            }
        }

        [NonEvent]
        public void ActivityStarted(Activity activity)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All))
            {
                // Accessing activity.Id here will cause the Id to be initialized
                // before the sampler runs in case where the activity is created using legacy way
                // i.e. new Activity("Operation name"). This will result in Id not reflecting the
                // correct sampling flags
                // https://github.com/dotnet/runtime/issues/61857
                var activityId = string.Concat("00-", activity.TraceId.ToHexString(), "-", activity.SpanId.ToHexString());
                activityId = string.Concat(activityId, activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "-01" : "-00");
                this.ActivityStarted(activity.OperationName, activityId);
            }
        }

        [NonEvent]
        public void ActivityStopped(Activity activity)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All))
            {
                this.ActivityStopped(activity.OperationName, activity.Id);
            }
        }

        [NonEvent]
        public void SelfDiagnosticsFileCreateException(string logDirectory, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.SelfDiagnosticsFileCreateException(logDirectory, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void TracerProviderException(string evnt, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.TracerProviderException(evnt, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void MeterProviderException(string methodName, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.MeterProviderException(methodName, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void MissingPermissionsToReadEnvironmentVariable(SecurityException ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.MissingPermissionsToReadEnvironmentVariable(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void DroppedExportProcessorItems(string exportProcessorName, string exporterName, long droppedCount)
        {
            if (droppedCount > 0)
            {
                if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
                {
                    this.ExistsDroppedExportProcessorItems(exportProcessorName, exporterName, droppedCount);
                }
            }
            else
            {
                if (this.IsEnabled(EventLevel.Informational, EventKeywords.All))
                {
                    this.NoDroppedExportProcessorItems(exportProcessorName, exporterName);
                }
            }
        }

        [Event(1, Message = "Span processor queue size reached maximum. Throttling spans.", Level = EventLevel.Warning)]
        public void SpanProcessorQueueIsExhausted()
        {
            this.WriteEvent(1);
        }

        [Event(2, Message = "Shutdown complete. '{0}' spans left in queue unprocessed.", Level = EventLevel.Informational)]
        public void ShutdownEvent(int spansLeftUnprocessed)
        {
            this.WriteEvent(2, spansLeftUnprocessed);
        }

        [Event(3, Message = "Exporter returned error '{0}'.", Level = EventLevel.Warning)]
        public void ExporterErrorResult(ExportResult exportResult)
        {
            this.WriteEvent(3, exportResult.ToString());
        }

        [Event(4, Message = "Unknown error in SpanProcessor event '{0}': '{1}'.", Level = EventLevel.Error)]
        public void SpanProcessorException(string evnt, string ex)
        {
            this.WriteEvent(4, evnt, ex);
        }

        [Event(5, Message = "Calling '{0}' on ended span.", Level = EventLevel.Warning)]
        public void UnexpectedCallOnEndedSpan(string methodName)
        {
            this.WriteEvent(5, methodName);
        }

        [Event(6, Message = "Attempting to dispose scope '{0}' that is not current", Level = EventLevel.Warning)]
        public void AttemptToEndScopeWhichIsNotCurrent(string spanName)
        {
            this.WriteEvent(6, spanName);
        }

        [Event(7, Message = "Attempting to activate span: '{0}'", Level = EventLevel.Informational)]
        public void AttemptToActivateActiveSpan(string spanName)
        {
            this.WriteEvent(7, spanName);
        }

        [Event(8, Message = "Calling method '{0}' with invalid argument '{1}', issue '{2}'.", Level = EventLevel.Warning)]
        public void InvalidArgument(string methodName, string argumentName, string issue)
        {
            this.WriteEvent(8, methodName, argumentName, issue);
        }

        [Event(10, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToInjectActivityContext(string format, string error)
        {
            this.WriteEvent(10, format, error);
        }

        [Event(11, Message = "Failed to parse tracestate: too many items", Level = EventLevel.Warning)]
        public void TooManyItemsInTracestate()
        {
            this.WriteEvent(11);
        }

        [Event(12, Message = "Tracestate key is invalid, key = '{0}'", Level = EventLevel.Warning)]
        public void TracestateKeyIsInvalid(string key)
        {
            this.WriteEvent(12, key);
        }

        [Event(13, Message = "Tracestate value is invalid, value = '{0}'", Level = EventLevel.Warning)]
        public void TracestateValueIsInvalid(string value)
        {
            this.WriteEvent(13, value);
        }

        [Event(14, Message = "Tracestate parse error: '{0}'", Level = EventLevel.Warning)]
        public void TracestateExtractError(string error)
        {
            this.WriteEvent(14, error);
        }

        [Event(15, Message = "Attempting to activate out-of-band span '{0}'", Level = EventLevel.Warning)]
        public void AttemptToActivateOobSpan(string spanName)
        {
            this.WriteEvent(15, spanName);
        }

        [Event(16, Message = "Exception occurred while invoking Observable instrument callback. Exception: '{0}'", Level = EventLevel.Warning)]
        public void ObservableInstrumentCallbackException(string exception)
        {
            this.WriteEvent(16, exception);
        }

        [Event(22, Message = "ForceFlush complete. '{0}' spans left in queue unprocessed.", Level = EventLevel.Informational)]
        public void ForceFlushCompleted(int spansLeftUnprocessed)
        {
            this.WriteEvent(22, spansLeftUnprocessed);
        }

        [Event(23, Message = "Timeout reached waiting on SpanExporter. '{0}' spans attempted.", Level = EventLevel.Warning)]
        public void SpanExporterTimeout(int spansAttempted)
        {
            this.WriteEvent(23, spansAttempted);
        }

        [Event(24, Message = "Activity started. OperationName = '{0}', Id = '{1}'.", Level = EventLevel.Verbose)]
        public void ActivityStarted(string operationName, string id)
        {
            this.WriteEvent(24, operationName, id);
        }

        [Event(25, Message = "Activity stopped. OperationName = '{0}', Id = '{1}'.", Level = EventLevel.Verbose)]
        public void ActivityStopped(string operationName, string id)
        {
            this.WriteEvent(25, operationName, id);
        }

        [Event(26, Message = "Failed to create file. LogDirectory ='{0}', Id = '{1}'.", Level = EventLevel.Warning)]
        public void SelfDiagnosticsFileCreateException(string logDirectory, string exception)
        {
            this.WriteEvent(26, logDirectory, exception);
        }

        [Event(28, Message = "Unknown error in TracerProvider '{0}': '{1}'.", Level = EventLevel.Error)]
        public void TracerProviderException(string evnt, string ex)
        {
            this.WriteEvent(28, evnt, ex);
        }

        [Event(29, Message = "Failed to parse environment variable: '{0}', value: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToParseEnvironmentVariable(string name, string value)
        {
            this.WriteEvent(29, name, value);
        }

        [Event(30, Message = "Missing permissions to read environment variable: '{0}'", Level = EventLevel.Warning)]
        public void MissingPermissionsToReadEnvironmentVariable(string exception)
        {
            this.WriteEvent(30, exception);
        }

        [Event(31, Message = "'{0}' exporting to '{1}' dropped '0' items.", Level = EventLevel.Informational)]
        public void NoDroppedExportProcessorItems(string exportProcessorName, string exporterName)
        {
            this.WriteEvent(31, exportProcessorName, exporterName);
        }

        [Event(32, Message = "'{0}' exporting to '{1}' dropped '{2}' item(s) due to buffer full.", Level = EventLevel.Warning)]
        public void ExistsDroppedExportProcessorItems(string exportProcessorName, string exporterName, long droppedCount)
        {
            this.WriteEvent(32, exportProcessorName, exporterName, droppedCount);
        }

        [Event(33, Message = "Measurements from Instrument '{0}', Meter '{1}' will be ignored. Reason: '{2}'. Suggested action: '{3}'", Level = EventLevel.Warning)]
        public void MetricInstrumentIgnored(string instrumentName, string meterName, string reason, string fix)
        {
            this.WriteEvent(33, instrumentName, meterName, reason, fix);
        }

        [Event(34, Message = "Unknown error in MetricReader event '{0}': '{1}'.", Level = EventLevel.Error)]
        public void MetricReaderException(string methodName, string ex)
        {
            this.WriteEvent(34, methodName, ex);
        }

        [Event(35, Message = "Unknown error in MeterProvider '{0}': '{1}'.", Level = EventLevel.Error)]
        public void MeterProviderException(string methodName, string ex)
        {
            this.WriteEvent(35, methodName, ex);
        }

        [Event(36, Message = "Measurement dropped from Instrument Name/Metric Stream Name '{0}'. Reason: '{1}'. Suggested action: '{2}'", Level = EventLevel.Warning)]
        public void MeasurementDropped(string instrumentName, string reason, string fix)
        {
            this.WriteEvent(36, instrumentName, reason, fix);
        }

        [Event(37, Message = "'{0}' Disposed.", Level = EventLevel.Informational)]
        public void ProviderDisposed(string providerName)
        {
            this.WriteEvent(37, providerName);
        }

        [Event(38, Message = "Duplicate Instrument '{0}', Meter '{1}' encountered. Reason: '{2}'. Suggested action: '{3}'", Level = EventLevel.Warning)]
        public void DuplicateMetricInstrument(string instrumentName, string meterName, string reason, string fix)
        {
            this.WriteEvent(38, instrumentName, meterName, reason, fix);
        }

        [Event(39, Message = "MeterProviderSdk event: '{0}'", Level = EventLevel.Verbose)]
        public void MeterProviderSdkEvent(string message)
        {
            this.WriteEvent(39, message);
        }

        [Event(40, Message = "MetricReader event: '{0}'", Level = EventLevel.Verbose)]
        public void MetricReaderEvent(string message)
        {
            this.WriteEvent(40, message);
        }

        [Event(41, Message = "View Configuration ignored for Instrument '{0}', Meter '{1}'. Reason: '{2}'. Measurements from the instrument will use default configuration for Aggregation. Suggested action: '{3}'", Level = EventLevel.Warning)]
        public void MetricViewIgnored(string instrumentName, string meterName, string reason, string fix)
        {
            this.WriteEvent(41, instrumentName, meterName, reason, fix);
        }

#if DEBUG
        public class OpenTelemetryEventListener : EventListener
        {
            private readonly List<EventSource> eventSources = new();

            public override void Dispose()
            {
                foreach (EventSource eventSource in this.eventSources)
                {
                    this.DisableEvents(eventSource);
                }

                base.Dispose();
                GC.SuppressFinalize(this);
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource?.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.eventSources.Add(eventSource);
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
                }

                base.OnEventSourceCreated(eventSource);
            }

            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
                string message;
                if (e.Message != null && (e.Payload?.Count ?? 0) > 0)
                {
                    message = string.Format(e.Message, e.Payload.ToArray());
                }
                else
                {
                    message = e.Message;
                }

                Debug.WriteLine($"{e.EventSource.Name} - EventId: [{e.EventId}], EventName: [{e.EventName}], Message: [{message}]");
            }
        }
#endif
    }
}
