// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Trace;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal static class ActivityExtensions
{
    private static readonly ConcurrentBag<ScopeSpans> SpanListPool = new();

    internal static void AddBatch(
        this ExportTraceServiceRequest request,
        SdkLimitOptions sdkLimitOptions,
        Resource processResource,
        in Batch<Activity> activityBatch)
    {
        Dictionary<string, ScopeSpans> spansByLibrary = new Dictionary<string, ScopeSpans>();
        ResourceSpans resourceSpans = new ResourceSpans
        {
            Resource = processResource,
        };
        request.ResourceSpans.Add(resourceSpans);

        var maxTags = sdkLimitOptions.AttributeCountLimit ?? int.MaxValue;

        foreach (var activity in activityBatch)
        {
            Span? span = activity.ToOtlpSpan(sdkLimitOptions);
            if (span == null)
            {
                OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateActivity(
                    nameof(ActivityExtensions),
                    nameof(AddBatch));
                continue;
            }

            var activitySourceName = activity.Source.Name;
            if (!spansByLibrary.TryGetValue(activitySourceName, out var scopeSpans))
            {
                scopeSpans = GetSpanListFromPool(activity.Source, maxTags, sdkLimitOptions.AttributeValueLengthLimit);

                spansByLibrary.Add(activitySourceName, scopeSpans);
                resourceSpans.ScopeSpans.Add(scopeSpans);
            }

            scopeSpans.Spans.Add(span);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(this ExportTraceServiceRequest request)
    {
        var resourceSpans = request.ResourceSpans.FirstOrDefault();
        if (resourceSpans == null)
        {
            return;
        }

        foreach (var scopeSpan in resourceSpans.ScopeSpans)
        {
            scopeSpan.Spans.Clear();
            SpanListPool.Add(scopeSpan);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ScopeSpans GetSpanListFromPool(ActivitySource activitySource, int maxTags, int? attributeValueLengthLimit)
    {
        if (!SpanListPool.TryTake(out var scopeSpans))
        {
            scopeSpans = new ScopeSpans
            {
                Scope = new InstrumentationScope
                {
                    Name = activitySource.Name, // Name is enforced to not be null, but it can be empty.
                    Version = activitySource.Version ?? string.Empty, // NRE throw by proto
                },
            };

            if (activitySource.Tags != null)
            {
                var scopeAttributes = scopeSpans.Scope.Attributes;

                foreach (var tag in activitySource.Tags ?? [])
                {
                    if (scopeAttributes.Count < maxTags)
                    {
                        OtlpTagWriter.Instance.TryWriteTag(ref scopeAttributes, tag, attributeValueLengthLimit);
                    }
                    else
                    {
                        scopeSpans.Scope.DroppedAttributesCount++;
                    }
                }
            }
        }

        return scopeSpans;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span? ToOtlpSpan(this Activity activity, SdkLimitOptions sdkLimitOptions)
    {
        if (activity.IdFormat != ActivityIdFormat.W3C)
        {
            // Only ActivityIdFormat.W3C is supported, in principle this should never be
            // hit under the OpenTelemetry SDK.
            return null;
        }

        byte[] traceIdBytes = new byte[16];
        byte[] spanIdBytes = new byte[8];

        activity.TraceId.CopyTo(traceIdBytes);
        activity.SpanId.CopyTo(spanIdBytes);

        var parentSpanIdString = ByteString.Empty;
        if (activity.ParentSpanId != default)
        {
            byte[] parentSpanIdBytes = new byte[8];
            activity.ParentSpanId.CopyTo(parentSpanIdBytes);
            parentSpanIdString = UnsafeByteOperations.UnsafeWrap(parentSpanIdBytes);
        }

        var startTimeUnixNano = activity.StartTimeUtc.ToUnixTimeNanoseconds();
        var otlpSpan = new Span
        {
            Name = activity.DisplayName,

            // There is an offset of 1 on the OTLP enum.
            Kind = (Span.Types.SpanKind)(activity.Kind + 1),

            TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes),
            SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes),
            ParentSpanId = parentSpanIdString,
            TraceState = activity.TraceStateString ?? string.Empty,

            StartTimeUnixNano = (ulong)startTimeUnixNano,
            EndTimeUnixNano = (ulong)(startTimeUnixNano + activity.Duration.ToNanoseconds()),
        };

        TagEnumerationState otlpTags = new()
        {
            SdkLimitOptions = sdkLimitOptions,
            Span = otlpSpan,
        };
        otlpTags.EnumerateTags(activity, sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue);

        if (activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer)
        {
            PeerServiceResolver.Resolve(ref otlpTags, out string? peerServiceName, out bool addAsTag);

            if (peerServiceName != null && addAsTag)
            {
                otlpSpan.Attributes.Add(
                    new KeyValue
                    {
                        Key = SemanticConventions.AttributePeerService,
                        Value = new AnyValue { StringValue = peerServiceName },
                    });
            }
        }

        otlpSpan.Status = activity.ToOtlpStatus(ref otlpTags);

        EventEnumerationState otlpEvents = new()
        {
            SdkLimitOptions = sdkLimitOptions,
            Span = otlpSpan,
        };
        otlpEvents.EnumerateEvents(activity, sdkLimitOptions.SpanEventCountLimit ?? int.MaxValue);

        LinkEnumerationState otlpLinks = new()
        {
            SdkLimitOptions = sdkLimitOptions,
            Span = otlpSpan,
        };
        otlpLinks.EnumerateLinks(activity, sdkLimitOptions.SpanLinkCountLimit ?? int.MaxValue);

        otlpSpan.Flags = ToOtlpSpanFlags(activity.Context.TraceFlags, activity.HasRemoteParent);

        return otlpSpan;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OtlpTrace.Status? ToOtlpStatus(this Activity activity, ref TagEnumerationState otlpTags)
    {
        var statusCodeForTagValue = StatusHelper.GetStatusCodeForTagValue(otlpTags.StatusCode);
        if (activity.Status == ActivityStatusCode.Unset && statusCodeForTagValue == null)
        {
            return null;
        }

        OtlpTrace.Status.Types.StatusCode otlpActivityStatusCode = OtlpTrace.Status.Types.StatusCode.Unset;
        string? otlpStatusDescription = null;
        if (activity.Status != ActivityStatusCode.Unset)
        {
            // The numerical values of the two enumerations match, a simple cast is enough.
            otlpActivityStatusCode = (OtlpTrace.Status.Types.StatusCode)(int)activity.Status;
            if (activity.Status == ActivityStatusCode.Error && !string.IsNullOrEmpty(activity.StatusDescription))
            {
                otlpStatusDescription = activity.StatusDescription;
            }
        }
        else
        {
            if (statusCodeForTagValue != StatusCode.Unset)
            {
                // The numerical values of the two enumerations match, a simple cast is enough.
                otlpActivityStatusCode = (OtlpTrace.Status.Types.StatusCode)(int)statusCodeForTagValue!;
                if (statusCodeForTagValue == StatusCode.Error && !string.IsNullOrEmpty(otlpTags.StatusDescription))
                {
                    otlpStatusDescription = otlpTags.StatusDescription;
                }
            }
        }

        var otlpStatus = new OtlpTrace.Status { Code = otlpActivityStatusCode };
        if (!string.IsNullOrEmpty(otlpStatusDescription))
        {
            otlpStatus.Message = otlpStatusDescription;
        }

        return otlpStatus;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span.Types.Link ToOtlpLink(in ActivityLink activityLink, SdkLimitOptions sdkLimitOptions)
    {
        byte[] traceIdBytes = new byte[16];
        byte[] spanIdBytes = new byte[8];

        activityLink.Context.TraceId.CopyTo(traceIdBytes);
        activityLink.Context.SpanId.CopyTo(spanIdBytes);

        var otlpLink = new Span.Types.Link
        {
            TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes),
            SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes),
        };

        int maxTags = sdkLimitOptions.SpanLinkAttributeCountLimit ?? int.MaxValue;

        var otlpLinkAttributes = otlpLink.Attributes;

        foreach (ref readonly var tag in activityLink.EnumerateTagObjects())
        {
            if (otlpLinkAttributes.Count == maxTags)
            {
                otlpLink.DroppedAttributesCount++;
                continue;
            }

            OtlpTagWriter.Instance.TryWriteTag(ref otlpLinkAttributes, tag, sdkLimitOptions.AttributeValueLengthLimit);
        }

        otlpLink.Flags = ToOtlpSpanFlags(activityLink.Context.TraceFlags, activityLink.Context.IsRemote);

        return otlpLink;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span.Types.Event ToOtlpEvent(in ActivityEvent activityEvent, SdkLimitOptions sdkLimitOptions)
    {
        var otlpEvent = new Span.Types.Event
        {
            Name = activityEvent.Name,
            TimeUnixNano = (ulong)activityEvent.Timestamp.ToUnixTimeNanoseconds(),
        };

        int maxTags = sdkLimitOptions.SpanEventAttributeCountLimit ?? int.MaxValue;

        var otlpEventAttributes = otlpEvent.Attributes;

        foreach (ref readonly var tag in activityEvent.EnumerateTagObjects())
        {
            if (otlpEventAttributes.Count == maxTags)
            {
                otlpEvent.DroppedAttributesCount++;
                continue;
            }

            OtlpTagWriter.Instance.TryWriteTag(ref otlpEventAttributes, tag, sdkLimitOptions.AttributeValueLengthLimit);
        }

        return otlpEvent;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToOtlpSpanFlags(ActivityTraceFlags activityTraceFlags, bool isRemote)
    {
        SpanFlags flags = (SpanFlags)activityTraceFlags;

        flags |= SpanFlags.ContextHasIsRemoteMask;

        if (isRemote)
        {
            flags |= SpanFlags.ContextIsRemoteMask;
        }

        return (uint)flags;
    }

    private struct TagEnumerationState : PeerServiceResolver.IPeerServiceState
    {
        public SdkLimitOptions SdkLimitOptions;

        public Span Span;

        public string? StatusCode;

        public string? StatusDescription;

        public string? PeerService { get; set; }

        public int? PeerServicePriority { get; set; }

        public string? HostName { get; set; }

        public string? IpAddress { get; set; }

        public long Port { get; set; }

        public void EnumerateTags(Activity activity, int maxTags)
        {
            var otlpSpanAttributes = this.Span.Attributes;

            foreach (ref readonly var tag in activity.EnumerateTagObjects())
            {
                if (tag.Value == null)
                {
                    continue;
                }

                var key = tag.Key;

                switch (key)
                {
                    case SpanAttributeConstants.StatusCodeKey:
                        this.StatusCode = tag.Value as string;
                        continue;
                    case SpanAttributeConstants.StatusDescriptionKey:
                        this.StatusDescription = tag.Value as string;
                        continue;
                }

                if (otlpSpanAttributes.Count == maxTags)
                {
                    this.Span.DroppedAttributesCount++;
                }
                else
                {
                    OtlpTagWriter.Instance.TryWriteTag(ref otlpSpanAttributes, tag, this.SdkLimitOptions.AttributeValueLengthLimit);
                }

                if (tag.Value is string tagStringValue)
                {
                    PeerServiceResolver.InspectTag(ref this, key, tagStringValue);
                }
                else if (tag.Value is int tagIntValue)
                {
                    PeerServiceResolver.InspectTag(ref this, key, tagIntValue);
                }
            }
        }
    }

    private struct EventEnumerationState
    {
        public SdkLimitOptions SdkLimitOptions;

        public Span Span;

        public void EnumerateEvents(Activity activity, int maxEvents)
        {
            foreach (ref readonly var @event in activity.EnumerateEvents())
            {
                if (this.Span.Events.Count < maxEvents)
                {
                    this.Span.Events.Add(ToOtlpEvent(in @event, this.SdkLimitOptions));
                }
                else
                {
                    this.Span.DroppedEventsCount++;
                }
            }
        }
    }

    private struct LinkEnumerationState
    {
        public SdkLimitOptions SdkLimitOptions;

        public Span Span;

        public void EnumerateLinks(Activity activity, int maxLinks)
        {
            foreach (ref readonly var link in activity.EnumerateLinks())
            {
                if (this.Span.Links.Count < maxLinks)
                {
                    this.Span.Links.Add(ToOtlpLink(in link, this.SdkLimitOptions));
                }
                else
                {
                    this.Span.DroppedLinksCount++;
                }
            }
        }
    }
}
