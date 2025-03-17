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
        string? parentId = activity.ParentSpanId == default ? null : EncodeSpanId(activity.ParentSpanId);

        var tags = PooledList<KeyValuePair<string, object?>>.Create();
        ExtractActivityTags(activity, ref tags);
        ExtractActivityStatus(activity, ref tags);
        ExtractActivitySource(activity, ref tags);

        ZipkinEndpoint? remoteEndpoint = ExtractRemoteEndpoint(activity, ref tags);
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
    {
        return spanId.ToHexString();
    }

    internal static long ToEpochMicroseconds(this DateTimeOffset dateTimeOffset)
    {
        // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
        // the last digit being off by one for dates that result in negative Unix times
        long microseconds = dateTimeOffset.Ticks / TicksPerMicrosecond;
        return microseconds - UnixEpochMicroseconds;
    }

    internal static long ToEpochMicroseconds(this TimeSpan timeSpan)
    {
        return timeSpan.Ticks / TicksPerMicrosecond;
    }

    internal static long ToEpochMicroseconds(this DateTime utcDateTime)
    {
        // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
        // the last digit being off by one for dates that result in negative Unix times
        long microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
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

    private static string? ToActivityKind(Activity activity)
    {
        return activity.Kind switch
        {
            ActivityKind.Server => "SERVER",
            ActivityKind.Producer => "PRODUCER",
            ActivityKind.Consumer => "CONSUMER",
            ActivityKind.Client => "CLIENT",
            _ => null,
        };
    }

    private static void ExtractActivityTags(Activity activity, ref PooledList<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key != ZipkinErrorFlagTagName && tag.Key != SpanAttributeConstants.StatusCodeKey)
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
                        activity.StatusDescription ?? string.Empty));
            }
        }
        else
        {
            var status = activity.GetTagItem(SpanAttributeConstants.StatusCodeKey) as string;
            if (status != null)
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
                        activity.StatusDescription ?? string.Empty));
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

    private static ZipkinEndpoint? ExtractRemoteEndpoint(Activity activity, ref PooledList<KeyValuePair<string, object?>> tags)
    {
        // Extract remote endpoint rule from https://github.com/open-telemetry/opentelemetry-specification/blob/v1.42.0/specification/trace/sdk_exporters/zipkin.md#remote-endpoint
        if (activity.Kind != ActivityKind.Client && activity.Kind != ActivityKind.Producer)
        {
            return null;
        }

        string? remoteEndpoint = activity.GetTagItem(SemanticConventions.AttributePeerService) as string;

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributeServerAddress) as string;

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributeNetPeerName) as string;

        if (remoteEndpoint == null)
        {
            var peerAddress = activity.GetTagItem(SemanticConventions.AttributeNetworkPeerAddress) as string;
            var peerPort = activity.GetTagItem(SemanticConventions.AttributeNetworkPeerPort) as string;

            if (peerAddress != null)
            {
                remoteEndpoint = peerPort != null ? $"{peerAddress}:{peerPort}" : peerAddress;
            }
        }

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributeServerSocketDomain) as string;

        if (remoteEndpoint == null)
        {
            var address = activity.GetTagItem(SemanticConventions.AttributeServerSocketAddress) as string;
            var port = activity.GetTagItem(SemanticConventions.AttributeServerSocketPort) as string;

            if (address != null)
            {
                remoteEndpoint = port != null ? $"{address}:{port}" : address;
            }
        }

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributeNetSockPeerName) as string;

        if (remoteEndpoint == null)
        {
            var socketAddress = activity.GetTagItem(SemanticConventions.AttributeNetSockPeerAddr) as string;
            var socketPort = activity.GetTagItem(SemanticConventions.AttributeNetSockPeerPort) as string;

            if (socketAddress != null)
            {
                remoteEndpoint = socketPort != null ? $"{socketAddress}:{socketPort}" : socketAddress;
            }
        }

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributePeerHostname) as string;

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributePeerAddress) as string;

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributeDbName) as string;

        remoteEndpoint ??= activity.GetTagItem(SemanticConventions.AttributeHttpHost) as string;

        if (remoteEndpoint != null)
        {
            var endpoint = RemoteEndpointCache.GetOrAdd((remoteEndpoint, default), ZipkinEndpoint.Create);
            PooledList<KeyValuePair<string, object?>>.Add(ref tags, new KeyValuePair<string, object?>(SemanticConventions.AttributePeerService, remoteEndpoint));
            return endpoint;
        }

        return null;
    }

    private static PooledList<ZipkinAnnotation> ExtractActivityEvents(Activity activity)
    {
        var annotations = PooledList<ZipkinAnnotation>.Create();
        foreach (var e in activity.Events)
        {
            PooledList<ZipkinAnnotation>.Add(ref annotations, new ZipkinAnnotation(e.Timestamp.ToEpochMicroseconds(), e.Name));
        }

        return annotations;
    }
}
