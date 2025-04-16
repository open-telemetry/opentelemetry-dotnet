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

    private static Dictionary<string, object?> cachedTags = [];

    internal static ZipkinSpan ToZipkinSpan(this Activity activity, ZipkinEndpoint localEndpoint, bool useShortTraceIds = false)
    {
        cachedTags = [];

        var context = activity.Context;
        string? parentId = activity.ParentSpanId == default ? null : EncodeSpanId(activity.ParentSpanId);

        var tags = PooledList<KeyValuePair<string, object?>>.Create();
        ExtractActivityTags(activity, ref tags);
        ExtractActivityStatus(activity, ref tags);
        ExtractActivitySource(activity, ref tags);

        ZipkinEndpoint? remoteEndpoint = ExtractRemoteEndpoint(activity);
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
        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            if (tag.Key != ZipkinErrorFlagTagName && tag.Key != SpanAttributeConstants.StatusCodeKey)
            {
                PooledList<KeyValuePair<string, object?>>.Add(ref tags, tag);
            }

            cachedTags[tag.Key] = tag.Value;
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
            if (GetTag(SpanAttributeConstants.StatusCodeKey) is string status)
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

    private static ZipkinEndpoint? ExtractRemoteEndpoint(Activity activity)
    {
        if (activity.Kind != ActivityKind.Client && activity.Kind != ActivityKind.Producer)
        {
            return null;
        }

        static ZipkinEndpoint? TryCreateEndpoint(string? remoteEndpoint)
        {
            if (remoteEndpoint != null)
            {
                return RemoteEndpointCache.GetOrAdd((remoteEndpoint, default), ZipkinEndpoint.Create);
            }

            return null;
        }

        var endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributePeerService));
        if (endpoint != null)
        {
            return endpoint;
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeServerAddress));
        if (endpoint != null)
        {
            return endpoint;
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeNetPeerName));
        if (endpoint != null)
        {
            return endpoint;
        }

        var peerAddress = GetTag(SemanticConventions.AttributeNetworkPeerAddress);
        var peerPort = GetTag(SemanticConventions.AttributeNetworkPeerPort);
        if (!string.IsNullOrEmpty(peerAddress))
        {
            var remoteEndpoint = !string.IsNullOrEmpty(peerPort) ? $"{peerAddress}:{peerPort}" : peerAddress;
            endpoint = TryCreateEndpoint(remoteEndpoint);
            if (endpoint != null)
            {
                return endpoint;
            }
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeServerSocketDomain));
        if (endpoint != null)
        {
            return endpoint;
        }

        var serverAddress = GetTag(SemanticConventions.AttributeServerSocketAddress);
        var serverPort = GetTag(SemanticConventions.AttributeServerSocketPort);
        if (!string.IsNullOrEmpty(serverAddress))
        {
            var remoteEndpoint = !string.IsNullOrEmpty(serverPort) ? $"{serverAddress}:{serverPort}" : serverAddress;
            endpoint = TryCreateEndpoint(remoteEndpoint);
            if (endpoint != null)
            {
                return endpoint;
            }
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeNetSockPeerName));
        if (endpoint != null)
        {
            return endpoint;
        }

        var sockAddr = GetTag(SemanticConventions.AttributeNetSockPeerAddr);
        var sockPort = GetTag(SemanticConventions.AttributeNetSockPeerPort);
        if (!string.IsNullOrEmpty(sockAddr))
        {
            var remoteEndpoint = !string.IsNullOrEmpty(sockPort) ? $"{sockAddr}:{sockPort}" : sockAddr;
            endpoint = TryCreateEndpoint(remoteEndpoint);
            if (endpoint != null)
            {
                return endpoint;
            }
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributePeerHostname));
        if (endpoint != null)
        {
            return endpoint;
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributePeerAddress));
        if (endpoint != null)
        {
            return endpoint;
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeDbName));
        if (endpoint != null)
        {
            return endpoint;
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeDbName));
        if (endpoint != null)
        {
            return endpoint;
        }

        endpoint = TryCreateEndpoint(GetTag(SemanticConventions.AttributeHttpHost));
        if (endpoint != null)
        {
            return endpoint;
        }

        return null;
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

    private static string? GetTag(string key) => cachedTags.TryGetValue(key, out var value) ? value as string : null;
}
