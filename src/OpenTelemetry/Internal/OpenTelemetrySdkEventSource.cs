// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Internal;

/// <summary>
/// EventSource implementation for OpenTelemetry SDK implementation.
/// </summary>
[EventSource(Name = "OpenTelemetry-Sdk")]
internal sealed class OpenTelemetrySdkEventSource : EventSource, IConfigurationExtensionsLogger
{
    public static readonly OpenTelemetrySdkEventSource Log = new();
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
            this.ActivityStarted(activity.DisplayName, activityId);
        }
    }

    [NonEvent]
    public void ActivityStopped(Activity activity)
    {
        if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All))
        {
            this.ActivityStopped(activity.DisplayName, activity.Id);
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

    [NonEvent]
    public void LoggerParseStateException<TState>(Exception exception)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.LoggerParseStateException(typeof(TState).FullName!, exception.ToInvariantString());
        }
    }

    [NonEvent]
    public void LoggerProviderException(string methodName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.LoggerProviderException(methodName, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void LoggerProcessStateSkipped<TState>()
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.LoggerProcessStateSkipped(
                typeof(TState).FullName!,
                "because it does not implement a supported interface (either IReadOnlyList<KeyValuePair<string, object>> or IEnumerable<KeyValuePair<string, object>>)");
        }
    }

    [NonEvent]
    public void MetricViewException(string source, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.MetricViewException(source, ex.ToInvariantString());
        }
    }

    [Event(4, Message = "Unknown error in SpanProcessor event '{0}': '{1}'.", Level = EventLevel.Error)]
    public void SpanProcessorException(string evnt, string ex)
    {
        this.WriteEvent(4, evnt, ex);
    }

    [Event(8, Message = "Calling method '{0}' with invalid argument '{1}', issue '{2}'.", Level = EventLevel.Warning)]
    public void InvalidArgument(string methodName, string argumentName, string issue)
    {
        this.WriteEvent(8, methodName, argumentName, issue);
    }

    [Event(16, Message = "Exception occurred while invoking Observable instrument callback. Exception: '{0}'", Level = EventLevel.Warning)]
    public void ObservableInstrumentCallbackException(string exception)
    {
        this.WriteEvent(16, exception);
    }

    [Event(24, Message = "Activity started. Name = '{0}', Id = '{1}'.", Level = EventLevel.Verbose)]
    public void ActivityStarted(string name, string id)
    {
        this.WriteEvent(24, name, id);
    }

    [Event(25, Message = "Activity stopped. Name = '{0}', Id = '{1}'.", Level = EventLevel.Verbose)]
    public void ActivityStopped(string name, string? id)
    {
        this.WriteEvent(25, name, id);
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

    [Event(31, Message = "'{0}' exporting to '{1}' dropped '0' items.", Level = EventLevel.Informational)]
    public void NoDroppedExportProcessorItems(string exportProcessorName, string exporterName)
    {
        this.WriteEvent(31, exportProcessorName, exporterName);
    }

#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe.")]
#endif
    [Event(32, Message = "'{0}' exporting to '{1}' dropped '{2}' item(s) due to buffer full.", Level = EventLevel.Warning)]
    public void ExistsDroppedExportProcessorItems(string exportProcessorName, string exporterName, long droppedCount)
    {
        this.WriteEvent(32, exportProcessorName, exporterName, droppedCount);
    }

#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe.")]
#endif
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

#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe.")]
#endif
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

#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe.")]
#endif
    [Event(41, Message = "View Configuration ignored for Instrument '{0}', Meter '{1}'. Reason: '{2}'. Measurements from the instrument will use default configuration for Aggregation. Suggested action: '{3}'", Level = EventLevel.Warning)]
    public void MetricViewIgnored(string instrumentName, string meterName, string reason, string fix)
    {
        this.WriteEvent(41, instrumentName, meterName, reason, fix);
    }

#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe.")]
#endif
    [Event(43, Message = "ForceFlush invoked for processor type '{0}' returned result '{1}'.", Level = EventLevel.Verbose)]
    public void ProcessorForceFlushInvoked(string processorType, bool result)
    {
        this.WriteEvent(43, processorType, result);
    }

    [Event(44, Message = "OpenTelemetryLoggerProvider event: '{0}'", Level = EventLevel.Verbose)]
    public void OpenTelemetryLoggerProviderEvent(string message)
    {
        this.WriteEvent(44, message);
    }

    [Event(45, Message = "ForceFlush invoked for OpenTelemetryLoggerProvider with timeoutMilliseconds = '{0}'.", Level = EventLevel.Verbose)]
    public void OpenTelemetryLoggerProviderForceFlushInvoked(int timeoutMilliseconds)
    {
        this.WriteEvent(45, timeoutMilliseconds);
    }

    [Event(46, Message = "TracerProviderSdk event: '{0}'", Level = EventLevel.Verbose)]
    public void TracerProviderSdkEvent(string message)
    {
        this.WriteEvent(46, message);
    }

    [Event(47, Message = "Configuration key '{0}' has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidConfigurationValue(string key, string? value)
    {
        this.WriteEvent(47, key, value);
    }

    [Event(48, Message = "Exception thrown parsing log state of type '{0}'. Exception: '{1}'", Level = EventLevel.Warning)]
    public void LoggerParseStateException(string type, string error)
    {
        this.WriteEvent(48, type, error);
    }

    [Event(49, Message = "LoggerProviderSdk event: '{0}'", Level = EventLevel.Verbose)]
    public void LoggerProviderSdkEvent(string message)
    {
        this.WriteEvent(49, message);
    }

    [Event(50, Message = "Unknown error in LoggerProvider '{0}': '{1}'.", Level = EventLevel.Error)]
    public void LoggerProviderException(string methodName, string ex)
    {
        this.WriteEvent(50, methodName, ex);
    }

    [Event(51, Message = "Skipped processing log state of type '{0}' {1}.", Level = EventLevel.Warning)]
    public void LoggerProcessStateSkipped(string type, string reason)
    {
        this.WriteEvent(51, type, reason);
    }

    [Event(52, Message = "Instrument '{0}', Meter '{1}' has been deactivated.", Level = EventLevel.Informational)]
    public void MetricInstrumentDeactivated(string instrumentName, string meterName)
    {
        this.WriteEvent(52, instrumentName, meterName);
    }

    [Event(53, Message = "Instrument '{0}', Meter '{1}' has been removed.", Level = EventLevel.Informational)]
    public void MetricInstrumentRemoved(string instrumentName, string meterName)
    {
        this.WriteEvent(53, instrumentName, meterName);
    }

    [Event(54, Message = "OTEL_TRACES_SAMPLER configuration was found but the value '{0}' is invalid and will be ignored.", Level = EventLevel.Warning)]
    public void TracesSamplerConfigInvalid(string configValue)
    {
        this.WriteEvent(54, configValue);
    }

    [Event(55, Message = "OTEL_TRACES_SAMPLER_ARG configuration was found but the value '{0}' is invalid and will be ignored, default of value of '1.0' will be used.", Level = EventLevel.Warning)]
    public void TracesSamplerArgConfigInvalid(string configValue)
    {
        this.WriteEvent(55, configValue);
    }

    [Event(56, Message = "Exception thrown by user code supplied on metric view ('{0}'): '{1}'.", Level = EventLevel.Error)]
    public void MetricViewException(string source, string ex)
    {
        this.WriteEvent(56, source, ex);
    }

    void IConfigurationExtensionsLogger.LogInvalidConfigurationValue(string key, string value)
    {
        this.InvalidConfigurationValue(key, value);
    }

#if DEBUG
    public class OpenTelemetryEventListener : EventListener
    {
        private readonly Dictionary<string, EventSource> eventSources = new();

        public override void Dispose()
        {
            foreach (var kvp in this.eventSources)
            {
                this.DisableEvents(kvp.Value);
            }

            base.Dispose();
            GC.SuppressFinalize(this);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
            {
                this.eventSources.Add(eventSource.Name, eventSource);
                this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!this.eventSources.ContainsKey(e.EventSource.Name))
            {
                return;
            }

            string? message;
            if (e.Message != null && e.Payload != null && e.Payload.Count > 0)
            {
                message = string.Format(System.Globalization.CultureInfo.CurrentCulture, e.Message, e.Payload.ToArray());
            }
            else
            {
                message = e.Message;
            }

            Debug.WriteLine($"{e.EventSource.Name} - Level: [{e.Level}], EventId: [{e.EventId}], EventName: [{e.EventName}], Message: [{message}]");
        }
    }
#endif
}
