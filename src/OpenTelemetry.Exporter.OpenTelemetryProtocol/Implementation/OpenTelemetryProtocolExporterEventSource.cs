// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

[EventSource(Name = "OpenTelemetry-Exporter-OpenTelemetryProtocol")]
internal sealed class OpenTelemetryProtocolExporterEventSource : EventSource, IConfigurationExtensionsLogger
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

    [NonEvent]
    public void TrySubmitRequestException(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.TrySubmitRequestException(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void RetryStoredRequestException(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.RetryStoredRequestException(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void TransientHttpError(Uri endpoint, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.TransientHttpError(endpoint.ToString(), ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void HttpRequestFailed(Uri endpoint, string? response, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.HttpRequestFailed(endpoint.ToString(), response, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void OperationUnexpectedlyCanceled(Uri endpoint, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.OperationUnexpectedlyCanceled(endpoint.ToString(), ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void RequestTimedOut(Uri endpoint, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.RequestTimedOut(endpoint.ToString(), ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void GrpcRetryDelayParsingFailed(string? grpcStatusDetailsHeader, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.GrpcRetryDelayParsingFailed(grpcStatusDetailsHeader ?? "null", ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void ExportFailure(Uri endpoint, string message, Status status)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExportFailure(endpoint.ToString(), message, status.ToString());
        }
    }

    [NonEvent]
    public void ExportSuccess(Uri endpoint, string message)
    {
        if (Log.IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            this.ExportSuccess(endpoint.ToString(), message);
        }
    }

    [NonEvent]
    public void ResponseDeserializationFailed(Uri endpoint)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ResponseDeserializationFailed(endpoint.ToString());
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

    [Event(11, Message = "Configuration key '{0}' has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidConfigurationValue(string key, string value)
    {
        this.WriteEvent(11, key, value);
    }

    [Event(12, Message = "Unknown error in TrySubmitRequest method. Message: '{0}'", Level = EventLevel.Error)]
    public void TrySubmitRequestException(string ex)
    {
        this.WriteEvent(12, ex);
    }

    [Event(13, Message = "Error while attempting to re-transmit data from disk. Message: '{0}'", Level = EventLevel.Error)]
    public void RetryStoredRequestException(string ex)
    {
        this.WriteEvent(13, ex);
    }

    [Event(14, Message = "{0} buffer exceeded the maximum allowed size. Current size: {1} bytes.", Level = EventLevel.Error)]
    public void BufferExceededMaxSize(string signalType, int length)
    {
        this.WriteEvent(14, signalType, length);
    }

    [Event(15, Message = "{0} buffer resizing failed due to insufficient memory.", Level = EventLevel.Error)]
    public void BufferResizeFailedDueToMemory(string signalType)
    {
        this.WriteEvent(15, signalType);
    }

    [Event(16, Message = "Transient HTTP error occurred when communicating with {0}. Exception: {1}", Level = EventLevel.Warning)]
    public void TransientHttpError(string endpoint, string exceptionMessage)
    {
        this.WriteEvent(16, endpoint, exceptionMessage);
    }

    [Event(17, Message = "HTTP request to {0} failed. Response: {1}. Exception: {2}", Level = EventLevel.Error)]
    public void HttpRequestFailed(string endpoint, string? response, string exceptionMessage)
    {
        this.WriteEvent(17, endpoint, response, exceptionMessage);
    }

    [Event(18, Message = "Operation unexpectedly canceled for endpoint {0}. Exception: {1}", Level = EventLevel.Warning)]
    public void OperationUnexpectedlyCanceled(string endpoint, string exceptionMessage)
    {
        this.WriteEvent(18, endpoint, exceptionMessage);
    }

    [Event(19, Message = "Request to endpoint {0} timed out. Exception: {1}", Level = EventLevel.Warning)]
    public void RequestTimedOut(string endpoint, string exceptionMessage)
    {
        this.WriteEvent(19, endpoint, exceptionMessage);
    }

    [Event(20, Message = "Failed to deserialize response from {0}.", Level = EventLevel.Error)]
    public void ResponseDeserializationFailed(string endpoint)
    {
        this.WriteEvent(20, endpoint);
    }

    [Event(21, Message = "Export succeeded for {0}. Message: {1}", Level = EventLevel.Informational)]
    public void ExportSuccess(string endpoint, string message)
    {
        this.WriteEvent(21, endpoint, message);
    }

    [Event(22, Message = "Export encountered GRPC status warning for {0}. Status code: {1}", Level = EventLevel.Warning)]
    public void GrpcStatusWarning(string endpoint, string statusCode)
    {
        this.WriteEvent(22, endpoint, statusCode);
    }

    [Event(23, Message = "Export failed for {0}. Message: {1}. {2}.", Level = EventLevel.Error)]
    public void ExportFailure(string endpoint, string message, string statusString)
    {
        this.WriteEvent(23, endpoint, message, statusString);
    }

    [Event(24, Message = "Failed to parse gRPC retry delay from header grpcStatusDetailsHeader: '{0}'. Exception: {1}", Level = EventLevel.Warning)]
    public void GrpcRetryDelayParsingFailed(string grpcStatusDetailsHeader, string exception)
    {
        this.WriteEvent(24, grpcStatusDetailsHeader, exception);
    }

    [Event(25, Message = "The array tag buffer exceeded the maximum allowed size. The array tag value was replaced with 'TRUNCATED'", Level = EventLevel.Warning)]
    public void ArrayBufferExceededMaxSize()
    {
        this.WriteEvent(25);
    }

    void IConfigurationExtensionsLogger.LogInvalidConfigurationValue(string key, string value)
    {
        this.InvalidConfigurationValue(key, value);
    }
}
