// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal static class ZipkinActivityConversionExtensions
{
    internal const string ZipkinErrorFlagTagName = "error";
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
    private const long UnixEpochTicks = 621355968000000000L; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks
    private const long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

    private static readonly ConcurrentDictionary<(string, int), ZipkinEndpoint> RemoteEndpointCache = new();

    internal static ZipkinSpan ToZipkinSpan(this Activity activity, ZipkinEndpoint localEndpoint, bool useShortTraceIds = false)
    {
        var context = activity.Context;
        var parentId = activity.ParentSpanId == default ? null : EncodeSpanId(activity.ParentSpanId);

        var tags = PooledList<KeyValuePair<string, object?>>.Create();
        ExtractActivityTags(activity, ref tags);
        ExtractActivityStatus(activity, ref tags);
        ExtractActivitySource(activity, ref tags);

        var remoteEndpoint = ExtractRemoteEndpoint(activity);
        var annotations = ExtractActivityEvents(activity);

        return new ZipkinSpan(
            EncodeTraceId(context.TraceId, useShortTraceIds),
            parentId,
            EncodeSpanId(context.SpanId),
            ToActivityKind(activity),
            activity.DisplayName,
            activity.StartTimeUtc.ToEpochMicroseconds(),
            duration: activity.Duration.ToEpochMicroseconds(),
            localEndpoint,
            remoteEndpoint,
            annotations,
            tags,
            null,
            null);
    }

    internal static string EncodeSpanId(ActivitySpanId spanId)
        => spanId.ToHexString();

    internal static long ToEpochMicroseconds(this DateTimeOffset dateTimeOffset)
    {
        // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
        // the last digit being off by one for dates that result in negative Unix times
        var microseconds = dateTimeOffset.Ticks / TicksPerMicrosecond;
        return microseconds - UnixEpochMicroseconds;
    }

    internal static long ToEpochMicroseconds(this TimeSpan timeSpan)
        => timeSpan.Ticks / TicksPerMicrosecond;

    internal static long ToEpochMicroseconds(this DateTime utcDateTime)
    {
        // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
        // the last digit being off by one for dates that result in negative Unix times
        var microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
        return microseconds - UnixEpochMicroseconds;
    }

    private static string EncodeTraceId(ActivityTraceId traceId, bool useShortTraceIds)
    {
        var id = traceId.ToHexString();

        if (id.Length > 16 && useShortTraceIds)
        {
            id = id.Substring(id.Length - 16, 16);
        }

        return id;
    }

    private static string? ToActivityKind(Activity activity) => activity.Kind switch
    {
        ActivityKind.Server => "SERVER",
        ActivityKind.Producer => "PRODUCER",
        ActivityKind.Consumer => "CONSUMER",
        ActivityKind.Client => "CLIENT",
        ActivityKind.Internal or _ => null,
    };

    private static string ExtractStatusDescription(Activity activity)
        => activity.StatusDescription
           ?? activity.GetTagItem(SpanAttributeConstants.StatusDescriptionKey) as string
           ?? activity.GetTagItem(ZipkinErrorFlagTagName) as string
           ?? string.Empty;

    private static void ExtractActivityTags(Activity activity, ref PooledList<KeyValuePair<string, object?>> tags)
    {
        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            if (tag.Key is not ZipkinErrorFlagTagName and not SpanAttributeConstants.StatusCodeKey)
            {
                PooledList<KeyValuePair<string, object?>>.Add(ref tags, tag);
            }
        }
    }

    private static void ExtractActivityStatus(Activity activity, ref PooledList<KeyValuePair<string, object?>> tags)
    {
        // When status is set on Activity using the native Status field in activity,
        // which was first introduced in System.Diagnostic.DiagnosticSource 6.0.0.
        if (activity.Status != ActivityStatusCode.Unset)
        {
            if (activity.Status == ActivityStatusCode.Ok)
            {
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tags,
                    new KeyValuePair<string, object?>(
                        SpanAttributeConstants.StatusCodeKey,
                        "OK"));
            }

            // activity.Status is Error
            else
            {
                var statusDescription = ExtractStatusDescription(activity);
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tags,
                    new KeyValuePair<string, object?>(
                        SpanAttributeConstants.StatusCodeKey,
                        "ERROR"));

                // Error flag rule from https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#status
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tags,
                    new KeyValuePair<string, object?>(
                        ZipkinErrorFlagTagName,
                        statusDescription));
            }
        }
        else
        {
            if (activity.GetTagItem(SpanAttributeConstants.StatusCodeKey) is string status)
            {
                if (status == "OK")
                {
                    activity.SetStatus(ActivityStatusCode.Ok);

                    PooledList<KeyValuePair<string, object?>>.Add(
                        ref tags,
                        new KeyValuePair<string, object?>(
                            SpanAttributeConstants.StatusCodeKey,
                            "OK"));
                }
                else if (status == "ERROR")
                {
                    var statusDescription = ExtractStatusDescription(activity);

                    activity.SetStatus(ActivityStatusCode.Error);

                    PooledList<KeyValuePair<string, object?>>.Add(
                    ref tags,
                    new KeyValuePair<string, object?>(
                        SpanAttributeConstants.StatusCodeKey,
                        "ERROR"));

                    PooledList<KeyValuePair<string, object?>>.Add(
                    ref tags,
                    new KeyValuePair<string, object?>(
                        ZipkinErrorFlagTagName,
                        statusDescription));
                }
            }
        }
    }

    private static void ExtractActivitySource(Activity activity, ref PooledList<KeyValuePair<string, object?>> tags)
    {
        var source = activity.Source;
        if (!string.IsNullOrEmpty(source.Name))
        {
            PooledList<KeyValuePair<string, object?>>.Add(ref tags, new KeyValuePair<string, object?>("otel.scope.name", source.Name));

            // otel.library.name is deprecated, but has to be propagated according to https://github.com/open-telemetry/opentelemetry-specification/blob/v1.31.0/specification/common/mapping-to-non-otlp.md#instrumentationscope
            PooledList<KeyValuePair<string, object?>>.Add(ref tags, new KeyValuePair<string, object?>("otel.library.name", source.Name));

            if (!string.IsNullOrEmpty(source.Version))
            {
                PooledList<KeyValuePair<string, object?>>.Add(ref tags, new KeyValuePair<string, object?>("otel.scope.version", source.Version));

                // otel.library.version is deprecated, but has to be propagated according to https://github.com/open-telemetry/opentelemetry-specification/blob/v1.31.0/specification/common/mapping-to-non-otlp.md#instrumentationscope
                PooledList<KeyValuePair<string, object?>>.Add(ref tags, new KeyValuePair<string, object?>("otel.library.version", source.Version));
            }
        }
    }

    private static ZipkinEndpoint? ExtractRemoteEndpoint(Activity activity)
    {
        if (activity.Kind is not ActivityKind.Client and not ActivityKind.Producer)
        {
            return null;
        }

        static ZipkinEndpoint? TryCreateEndpoint(string? remoteEndpoint)
        {
            if (remoteEndpoint != null)
            {
                var endpoint = RemoteEndpointCache.GetOrAdd((remoteEndpoint, default), ZipkinEndpoint.Create);
                return endpoint;
            }

            return null;
        }

        var remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributePeerService) as string;
        var endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeServerAddress) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeNetPeerName) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        var peerAddress = activity.GetTagItem(SemanticConventions.AttributeNetworkPeerAddress) as string;
        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeNetworkPeerPort) is string peerPort ? $"{peerAddress}:{peerPort}" : peerAddress;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeServerSocketDomain) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        var serverAddress = activity.GetTagItem(SemanticConventions.AttributeServerSocketAddress) as string;
        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeServerSocketPort) is string serverPort ? $"{serverAddress}:{serverPort}" : serverAddress;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeNetSockPeerName) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        var socketAddress = activity.GetTagItem(SemanticConventions.AttributeNetSockPeerAddr) as string;
        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeNetSockPeerPort) is string socketPort ? $"{socketAddress}:{socketPort}" : socketAddress;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributePeerHostname) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributePeerAddress) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        if (endpoint != null)
        {
            return endpoint;
        }

        remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributeDbName) as string;
        endpoint = TryCreateEndpoint(remoteEndpoint);
        return endpoint ?? null;
    }

    private static PooledList<ZipkinAnnotation> ExtractActivityEvents(Activity activity)
    {
        var enumerator = activity.EnumerateEvents().GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return default;
        }

        var annotations = PooledList<ZipkinAnnotation>.Create();

        do
        {
            var @event = enumerator.Current;
            PooledList<ZipkinAnnotation>.Add(ref annotations, new ZipkinAnnotation(@event.Timestamp.ToEpochMicroseconds(), @event.Name));
        }
        while (enumerator.MoveNext());

        return annotations;
    }
}
