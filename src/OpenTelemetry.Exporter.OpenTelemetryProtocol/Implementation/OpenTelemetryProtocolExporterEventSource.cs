// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

[EventSource(Name = "OpenTelemetry-Exporter-OpenTelemetryProtocol")]
internal sealed class OpenTelemetryProtocolExporterEventSource : EventSource
{
    public static readonly OpenTelemetryProtocolExporterEventSource Log = new();

    [NonEvent]
    public void FailedToReachCollector(Uri collectorUri, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            var rawCollectorUri = collectorUri.ToString();
            this.FailedToReachCollector(rawCollectorUri, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void ExportMethodException(Exception ex, bool isRetry = false)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExportMethodException(ex.ToInvariantString(), isRetry);
        }
    }

    [Event(2, Message = "Exporter failed send data to collector to {0} endpoint. Data will not be sent. Exception: {1}", Level = EventLevel.Error)]
    public void FailedToReachCollector(string rawCollectorUri, string ex)
    {
        this.WriteEvent(2, rawCollectorUri, ex);
    }

    [Event(3, Message = "Could not translate activity from class '{0}' and method '{1}', span will not be recorded.", Level = EventLevel.Informational)]
    public void CouldNotTranslateActivity(string className, string methodName)
    {
        this.WriteEvent(3, className, methodName);
    }

#if NET6_0_OR_GREATER
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe.")]
#endif
    [Event(4, Message = "Unknown error in export method. Message: '{0}'. IsRetry: {1}", Level = EventLevel.Error)]
    public void ExportMethodException(string ex, bool isRetry)
    {
        this.WriteEvent(4, ex, isRetry);
    }

    [Event(5, Message = "Could not translate metric from class '{0}' and method '{1}', metric will not be recorded.", Level = EventLevel.Informational)]
    public void CouldNotTranslateMetric(string className, string methodName)
    {
        this.WriteEvent(5, className, methodName);
    }

    [Event(8, Message = "Unsupported value for protocol '{0}' is configured, default protocol 'grpc' will be used.", Level = EventLevel.Warning)]
    public void UnsupportedProtocol(string protocol)
    {
        this.WriteEvent(8, protocol);
    }

    [Event(9, Message = "Could not translate LogRecord due to Exception: '{0}'. Log will not be exported.", Level = EventLevel.Warning)]
    public void CouldNotTranslateLogRecord(string exceptionMessage)
    {
        this.WriteEvent(9, exceptionMessage);
    }

    [Event(10, Message = "Unsupported attribute type '{0}' for '{1}'. Attribute will not be exported.", Level = EventLevel.Warning)]
    public void UnsupportedAttributeType(string type, string key)
    {
        this.WriteEvent(10, type.ToString(), key);
    }

    [Event(11, Message = "{0} environment variable has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidEnvironmentVariable(string key, string value)
    {
        this.WriteEvent(11, key, value);
    }
}
