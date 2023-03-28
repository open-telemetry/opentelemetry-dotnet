// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
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

            foreach (var activity in activityBatch)
            {
                Span span = activity.ToOtlpSpan(sdkLimitOptions);
                if (span == null)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateActivity(
                        nameof(ActivityExtensions),
                        nameof(AddBatch));
                    continue;
                }

                var activitySourceName = activity.Source.Name;
                if (!spansByLibrary.TryGetValue(activitySourceName, out var spans))
                {
                    spans = GetSpanListFromPool(activitySourceName, activity.Source.Version);

                    spansByLibrary.Add(activitySourceName, spans);
                    resourceSpans.ScopeSpans.Add(spans);
                }

                spans.Spans.Add(span);
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

            foreach (var scope in resourceSpans.ScopeSpans)
            {
                scope.Spans.Clear();
                SpanListPool.Add(scope);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ScopeSpans GetSpanListFromPool(string name, string version)
        {
            if (!SpanListPool.TryTake(out var spans))
            {
                spans = new ScopeSpans
                {
                    Scope = new InstrumentationScope
                    {
                        Name = name, // Name is enforced to not be null, but it can be empty.
                        Version = version ?? string.Empty, // NRE throw by proto
                    },
                };
            }
            else
            {
                spans.Scope.Name = name;
                spans.Scope.Version = version ?? string.Empty;
            }

            return spans;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Span ToOtlpSpan(this Activity activity, SdkLimitOptions sdkLimitOptions)
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
                PeerServiceResolver.Resolve(ref otlpTags, out string peerServiceName, out bool addAsTag);

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

            return otlpSpan;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpTrace.Status ToOtlpStatus(this Activity activity, ref TagEnumerationState otlpTags)
        {
            var statusCodeForTagValue = StatusHelper.GetStatusCodeForTagValue(otlpTags.StatusCode);
            if (activity.Status == ActivityStatusCode.Unset && statusCodeForTagValue == null)
            {
                return null;
            }

            OtlpTrace.Status.Types.StatusCode otlpActivityStatusCode = OtlpTrace.Status.Types.StatusCode.Unset;
            string otlpStatusDescription = null;
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
                    otlpActivityStatusCode = (OtlpTrace.Status.Types.StatusCode)(int)statusCodeForTagValue;
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
            foreach (ref readonly var tag in activityLink.EnumerateTagObjects())
            {
                if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var attribute, sdkLimitOptions.AttributeValueLengthLimit))
                {
                    if (otlpLink.Attributes.Count < maxTags)
                    {
                        otlpLink.Attributes.Add(attribute);
                    }
                    else
                    {
                        otlpLink.DroppedAttributesCount++;
                    }
                }
            }

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
            foreach (ref readonly var tag in activityEvent.EnumerateTagObjects())
            {
                if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var attribute, sdkLimitOptions.AttributeValueLengthLimit))
                {
                    if (otlpEvent.Attributes.Count < maxTags)
                    {
                        otlpEvent.Attributes.Add(attribute);
                    }
                    else
                    {
                        otlpEvent.DroppedAttributesCount++;
                    }
                }
            }

            return otlpEvent;
        }

        private struct TagEnumerationState : PeerServiceResolver.IPeerServiceState
        {
            public SdkLimitOptions SdkLimitOptions;

            public Span Span;

            public string StatusCode;

            public string StatusDescription;

            public string PeerService { get; set; }

            public int? PeerServicePriority { get; set; }

            public string HostName { get; set; }

            public string IpAddress { get; set; }

            public long Port { get; set; }

            public void EnumerateTags(Activity activity, int maxTags)
            {
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

                    if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var attribute, this.SdkLimitOptions.AttributeValueLengthLimit))
                    {
                        if (this.Span.Attributes.Count < maxTags)
                        {
                            this.Span.Attributes.Add(attribute);
                        }
                        else
                        {
                            this.Span.DroppedAttributesCount++;
                        }

                        if (attribute.Value.ValueCase == AnyValue.ValueOneofCase.StringValue)
                        {
                            // Note: tag.Value is used and not attribute.Value here because attribute.Value may be truncated
                            PeerServiceResolver.InspectTag(ref this, key, tag.Value as string);
                        }
                        else if (attribute.Value.ValueCase == AnyValue.ValueOneofCase.IntValue)
                        {
                            PeerServiceResolver.InspectTag(ref this, key, attribute.Value.IntValue);
                        }
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
}
