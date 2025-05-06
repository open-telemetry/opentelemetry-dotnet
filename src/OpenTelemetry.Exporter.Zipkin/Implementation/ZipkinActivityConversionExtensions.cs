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

        string? parentId = activity.ParentSpanId == default
            ? null
            : EncodeSpanId(activity.ParentSpanId);

        var tagState = new TagEnumerationState { Tags = PooledList<KeyValuePair<string, object?>>.Create(), };

        tagState.EnumerateTags(activity);

        // When status is set on Activity using the native Status field in activity,
        // which was first introduced in System.Diagnostic.DiagnosticSource 6.0.0.
        if (activity.Status != ActivityStatusCode.Unset)
        {
            if (activity.Status == ActivityStatusCode.Ok)
            {
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tagState.Tags,
                    new KeyValuePair<string, object?>(
                        SpanAttributeConstants.StatusCodeKey,
                        "OK"));
            }

            // activity.Status is Error
            else
            {
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tagState.Tags,
                    new KeyValuePair<string, object?>(
                        SpanAttributeConstants.StatusCodeKey,
                        "ERROR"));

                // Error flag rule from https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#status
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tagState.Tags,
                    new KeyValuePair<string, object?>(
                        ZipkinErrorFlagTagName,
                        activity.StatusDescription ?? string.Empty));
            }
        }

        // In the case when both activity status and status tag were set,
        // activity status takes precedence over status tag.
        else if (tagState.StatusCode.HasValue && tagState.StatusCode != StatusCode.Unset)
        {
            PooledList<KeyValuePair<string, object?>>.Add(
                ref tagState.Tags,
                new KeyValuePair<string, object?>(
                    SpanAttributeConstants.StatusCodeKey,
                    StatusHelper.GetTagValueForStatusCode(tagState.StatusCode.Value)));

            // Error flag rule from https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#status
            if (tagState.StatusCode == StatusCode.Error)
            {
                PooledList<KeyValuePair<string, object?>>.Add(
                    ref tagState.Tags,
                    new KeyValuePair<string, object?>(
                        ZipkinErrorFlagTagName,
                        tagState.StatusDescription ?? string.Empty));
            }
        }

        var activitySource = activity.Source;
        if (!string.IsNullOrEmpty(activitySource.Name))
        {
            PooledList<KeyValuePair<string, object?>>.Add(ref tagState.Tags, new KeyValuePair<string, object?>("otel.scope.name", activitySource.Name));

            // otel.library.name is deprecated, but has to be propagated according to https://github.com/open-telemetry/opentelemetry-specification/blob/v1.31.0/specification/common/mapping-to-non-otlp.md#instrumentationscope
            PooledList<KeyValuePair<string, object?>>.Add(ref tagState.Tags, new KeyValuePair<string, object?>("otel.library.name", activitySource.Name));
            if (!string.IsNullOrEmpty(activitySource.Version))
            {
                PooledList<KeyValuePair<string, object?>>.Add(ref tagState.Tags, new KeyValuePair<string, object?>("otel.scope.version", activitySource.Version));

                // otel.library.version is deprecated, but has to be propagated according to https://github.com/open-telemetry/opentelemetry-specification/blob/v1.31.0/specification/common/mapping-to-non-otlp.md#instrumentationscope
                PooledList<KeyValuePair<string, object?>>.Add(ref tagState.Tags, new KeyValuePair<string, object?>("otel.library.version", activitySource.Version));
            }
        }

        ZipkinEndpoint? remoteEndpoint = null;
        if (activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer)
        {
            PeerServiceResolver.Resolve(ref tagState, out string? peerServiceName, out bool addAsTag);

            if (peerServiceName != null)
            {
                remoteEndpoint = RemoteEndpointCache.GetOrAdd((peerServiceName, default), ZipkinEndpoint.Create);
                if (addAsTag)
                {
                    PooledList<KeyValuePair<string, object?>>.Add(ref tagState.Tags, new KeyValuePair<string, object?>(SemanticConventions.AttributePeerService, peerServiceName));
                }
            }
        }

        EventEnumerationState eventState = default;
        eventState.EnumerateEvents(activity);

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
            eventState.Annotations,
            tagState.Tags,
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

    internal struct TagEnumerationState : PeerServiceResolver.IPeerServiceState
    {
        public PooledList<KeyValuePair<string, object?>> Tags;

        public string? PeerService { get; set; }

        public int? PeerServicePriority { get; set; }

        public string? HostName { get; set; }

        public string? IpAddress { get; set; }

        public long Port { get; set; }

        public StatusCode? StatusCode { get; set; }

        public string StatusDescription { get; set; }

        public void EnumerateTags(Activity activity)
        {
            this.ProcessSourceTags(activity);
            this.ProcessActivityTags(activity);
        }

        private void ProcessSourceTags(Activity activity)
        {
            if (activity.Source.Tags is null || !activity.Source.Tags.Any())
            {
                return;
            }

            foreach (var sourceTag in activity.Source.Tags)
            {
                if (sourceTag.Value == null)
                {
                    continue; // Skip null values instead of returning
                }

                if (this.ProcessSpecialTag(sourceTag.Key, sourceTag.Value))
                {
                    continue;
                }

                PooledList<KeyValuePair<string, object?>>.Add(ref this.Tags, new KeyValuePair<string, object?>($"instrumentation.scope.{sourceTag.Key}", sourceTag.Value));
            }
        }

        private void ProcessActivityTags(Activity activity)
        {
            foreach (ref readonly var tag in activity.EnumerateTagObjects())
            {
                if (tag.Value == null)
                {
                    continue;
                }

                if (this.ProcessSpecialTag(tag.Key, tag.Value))
                {
                    continue;
               }

                PooledList<KeyValuePair<string, object?>>.Add(ref this.Tags, tag);
            }
        }

        private bool ProcessSpecialTag(string key, object? value)
        {
            // Process peer service related values
            if (value is string strVal)
            {
                PeerServiceResolver.InspectTag(ref this, key, strVal);

                // Handle status related tags
                if (key == SpanAttributeConstants.StatusCodeKey)
                {
                    this.StatusCode = StatusHelper.GetStatusCodeForTagValue(strVal);
                    return true;
                }

                if (key == SpanAttributeConstants.StatusDescriptionKey)
                {
                    // Description is sent as `error` but only if StatusCode is Error
                    this.StatusDescription = strVal;
                    return true;
                }

                if (key == ZipkinErrorFlagTagName)
                {
                    // Ignore `error` tag; it will be added based on StatusCode + StatusDescription
                    return true;
                }
            }
            else if (value is int intVal && key == SemanticConventions.AttributeNetPeerPort)
            {
                PeerServiceResolver.InspectTag(ref this, key, intVal);
            }

            // Tag was not special or fully processed
            return false;
        }
    }

    private struct EventEnumerationState
    {
        public PooledList<ZipkinAnnotation> Annotations;

        public void EnumerateEvents(Activity activity)
        {
            var enumerator = activity.EnumerateEvents();

            if (enumerator.MoveNext())
            {
                this.Annotations = PooledList<ZipkinAnnotation>.Create();

                do
                {
                    ref readonly var @event = ref enumerator.Current;

                    PooledList<ZipkinAnnotation>.Add(ref this.Annotations, new ZipkinAnnotation(@event.Timestamp.ToEpochMicroseconds(), @event.Name));
                }
                while (enumerator.MoveNext());
            }
        }
    }
}
