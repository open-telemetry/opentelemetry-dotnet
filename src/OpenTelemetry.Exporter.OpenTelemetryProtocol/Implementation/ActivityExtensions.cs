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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Configuration;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class ActivityExtensions
    {
        private static readonly ConcurrentBag<OtlpTrace.ScopeSpans> SpanListPool = new();
        private static readonly Action<RepeatedField<OtlpTrace.Span>, int> RepeatedFieldOfSpanSetCountAction = CreateRepeatedFieldOfSpanSetCountAction();

        internal static void AddBatch(
            this OtlpCollector.ExportTraceServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<Activity> activityBatch)
        {
            Dictionary<string, OtlpTrace.ScopeSpans> spansByLibrary = new Dictionary<string, OtlpTrace.ScopeSpans>();
            OtlpTrace.ResourceSpans resourceSpans = new OtlpTrace.ResourceSpans
            {
                Resource = processResource,
            };
            request.ResourceSpans.Add(resourceSpans);

            foreach (var activity in activityBatch)
            {
                OtlpTrace.Span span = activity.ToOtlpSpan();
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
        internal static void Return(this OtlpCollector.ExportTraceServiceRequest request)
        {
            var resourceSpans = request.ResourceSpans.FirstOrDefault();
            if (resourceSpans == null)
            {
                return;
            }

            foreach (var scope in resourceSpans.ScopeSpans)
            {
                RepeatedFieldOfSpanSetCountAction(scope.Spans, 0);
                SpanListPool.Add(scope);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpTrace.ScopeSpans GetSpanListFromPool(string name, string version)
        {
            if (!SpanListPool.TryTake(out var spans))
            {
                spans = new OtlpTrace.ScopeSpans
                {
                    Scope = new OtlpCommon.InstrumentationScope
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
        internal static OtlpTrace.Span ToOtlpSpan(this Activity activity)
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
            var otlpSpan = new OtlpTrace.Span
            {
                Name = activity.DisplayName,

                // There is an offset of 1 on the OTLP enum.
                Kind = (OtlpTrace.Span.Types.SpanKind)(activity.Kind + 1),

                TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes),
                SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes),
                ParentSpanId = parentSpanIdString,

                StartTimeUnixNano = (ulong)startTimeUnixNano,
                EndTimeUnixNano = (ulong)(startTimeUnixNano + activity.Duration.ToNanoseconds()),
            };

            TagEnumerationState otlpTags = new TagEnumerationState
            {
                Tags = PooledList<OtlpCommon.KeyValue>.Create(),
            };

            otlpTags.EnumerateTags(activity, SdkConfiguration.Instance.SpanAttributeCountLimit ?? int.MaxValue);

            if (activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer)
            {
                PeerServiceResolver.Resolve(ref otlpTags, out string peerServiceName, out bool addAsTag);

                if (peerServiceName != null && addAsTag)
                {
                    PooledList<OtlpCommon.KeyValue>.Add(
                        ref otlpTags.Tags,
                        new OtlpCommon.KeyValue
                        {
                            Key = SemanticConventions.AttributePeerService,
                            Value = new OtlpCommon.AnyValue { StringValue = peerServiceName },
                        });
                }
            }

            otlpSpan.Attributes.AddRange(otlpTags.Tags);
            otlpTags.Tags.Return();

            otlpSpan.Status = activity.ToOtlpStatus(ref otlpTags);

            EventEnumerationState otlpEvents = default;
            otlpEvents.EnumerateEvents(activity, SdkConfiguration.Instance.SpanEventCountLimit ?? int.MaxValue);
            if (otlpEvents.Created)
            {
                otlpSpan.Events.AddRange(otlpEvents.Events);
                otlpEvents.Events.Return();
            }

            LinkEnumerationState otlpLinks = default;
            otlpLinks.EnumerateLinks(activity, SdkConfiguration.Instance.SpanLinkCountLimit ?? int.MaxValue);
            if (otlpLinks.Created)
            {
                otlpSpan.Links.AddRange(otlpLinks.Links);
                otlpLinks.Links.Return();
            }

            // TODO: The drop counts should be set when necessary.

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
        private static OtlpTrace.Span.Types.Link ToOtlpLink(in ActivityLink activityLink)
        {
            byte[] traceIdBytes = new byte[16];
            byte[] spanIdBytes = new byte[8];

            activityLink.Context.TraceId.CopyTo(traceIdBytes);
            activityLink.Context.SpanId.CopyTo(spanIdBytes);

            var otlpLink = new OtlpTrace.Span.Types.Link
            {
                TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes),
                SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes),
            };

            var enumerator = activityLink.EnumerateTagObjects();
            if (enumerator.MoveNext())
            {
                int maxTags = SdkConfiguration.Instance.LinkAttributeCountLimit ?? int.MaxValue;
                if (maxTags > 0)
                {
                    do
                    {
                        ref readonly var tag = ref enumerator.Current;
                        if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var attribute, SdkConfiguration.Instance.AttributeValueLengthLimit))
                        {
                            otlpLink.Attributes.Add(attribute);
                        }
                    }
                    while (enumerator.MoveNext() && otlpLink.Attributes.Count <= maxTags);
                }
            }

            return otlpLink;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpTrace.Span.Types.Event ToOtlpEvent(in ActivityEvent activityEvent)
        {
            var otlpEvent = new OtlpTrace.Span.Types.Event
            {
                Name = activityEvent.Name,
                TimeUnixNano = (ulong)activityEvent.Timestamp.ToUnixTimeNanoseconds(),
            };

            var enumerator = activityEvent.EnumerateTagObjects();
            if (enumerator.MoveNext())
            {
                int maxTags = SdkConfiguration.Instance.EventAttributeCountLimit ?? int.MaxValue;
                if (maxTags > 0)
                {
                    do
                    {
                        ref readonly var tag = ref enumerator.Current;
                        if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var attribute, SdkConfiguration.Instance.AttributeValueLengthLimit))
                        {
                            otlpEvent.Attributes.Add(attribute);
                        }
                    }
                    while (enumerator.MoveNext() && otlpEvent.Attributes.Count <= maxTags);
                }
            }

            return otlpEvent;
        }

        private static Action<RepeatedField<OtlpTrace.Span>, int> CreateRepeatedFieldOfSpanSetCountAction()
        {
            FieldInfo repeatedFieldOfSpanCountField = typeof(RepeatedField<OtlpTrace.Span>).GetField("count", BindingFlags.NonPublic | BindingFlags.Instance);

            DynamicMethod dynamicMethod = new DynamicMethod(
                "CreateSetCountAction",
                null,
                new[] { typeof(RepeatedField<OtlpTrace.Span>), typeof(int) },
                typeof(ActivityExtensions).Module,
                skipVisibility: true);

            var generator = dynamicMethod.GetILGenerator();

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, repeatedFieldOfSpanCountField);
            generator.Emit(OpCodes.Ret);

            return (Action<RepeatedField<OtlpTrace.Span>, int>)dynamicMethod.CreateDelegate(typeof(Action<RepeatedField<OtlpTrace.Span>, int>));
        }

        private struct TagEnumerationState : PeerServiceResolver.IPeerServiceState
        {
            public PooledList<OtlpCommon.KeyValue> Tags;

            public string StatusCode;

            public string StatusDescription;

            public string PeerService { get; set; }

            public int? PeerServicePriority { get; set; }

            public string HostName { get; set; }

            public string IpAddress { get; set; }

            public long Port { get; set; }

            public void EnumerateTags(Activity activity, int maxTags)
            {
                if (maxTags <= 0)
                {
                    return;
                }

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

                    if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var attribute, SdkConfiguration.Instance.AttributeValueLengthLimit))
                    {
                        if (this.Tags.Count <= maxTags)
                        {
                            PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, attribute);
                        }

                        if (attribute.Value.ValueCase == OtlpCommon.AnyValue.ValueOneofCase.StringValue)
                        {
                            PeerServiceResolver.InspectTag(ref this, key, attribute.Value.StringValue);
                        }
                        else if (attribute.Value.ValueCase == OtlpCommon.AnyValue.ValueOneofCase.IntValue)
                        {
                            PeerServiceResolver.InspectTag(ref this, key, attribute.Value.IntValue);
                        }
                    }
                }
            }
        }

        private struct EventEnumerationState
        {
            public bool Created;

            public PooledList<OtlpTrace.Span.Types.Event> Events;

            public void EnumerateEvents(Activity activity, int maxEvents)
            {
                if (maxEvents <= 0)
                {
                    return;
                }

                var enumerator = activity.EnumerateEvents();

                if (enumerator.MoveNext())
                {
                    this.Events = PooledList<OtlpTrace.Span.Types.Event>.Create();
                    this.Created = true;

                    do
                    {
                        ref readonly var @event = ref enumerator.Current;
                        PooledList<OtlpTrace.Span.Types.Event>.Add(ref this.Events, ToOtlpEvent(in @event));
                    }
                    while (enumerator.MoveNext() && this.Events.Count <= maxEvents);
                }
            }
        }

        private struct LinkEnumerationState
        {
            public bool Created;

            public PooledList<OtlpTrace.Span.Types.Link> Links;

            public void EnumerateLinks(Activity activity, int maxLinks)
            {
                if (maxLinks <= 0)
                {
                    return;
                }

                var enumerator = activity.EnumerateLinks();

                if (enumerator.MoveNext())
                {
                    this.Links = PooledList<OtlpTrace.Span.Types.Link>.Create();
                    this.Created = true;

                    do
                    {
                        ref readonly var link = ref enumerator.Current;
                        PooledList<OtlpTrace.Span.Types.Link>.Add(ref this.Links, ToOtlpLink(in link));
                    }
                    while (enumerator.MoveNext() && this.Links.Count <= maxLinks);
                }
            }
        }
    }
}
