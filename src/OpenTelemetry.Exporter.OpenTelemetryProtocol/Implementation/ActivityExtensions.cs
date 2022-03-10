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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class ActivityExtensions
    {
        private static readonly ConcurrentBag<OtlpTrace.InstrumentationLibrarySpans> SpanListPool = new();
        private static readonly Action<RepeatedField<OtlpTrace.Span>, int> RepeatedFieldOfSpanSetCountAction = CreateRepeatedFieldOfSpanSetCountAction();

        internal static void AddBatch(
            this OtlpCollector.ExportTraceServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<Activity> activityBatch)
        {
            Dictionary<string, OtlpTrace.InstrumentationLibrarySpans> spansByLibrary = new Dictionary<string, OtlpTrace.InstrumentationLibrarySpans>();
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
                    resourceSpans.InstrumentationLibrarySpans.Add(spans);
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

            foreach (var librarySpans in resourceSpans.InstrumentationLibrarySpans)
            {
                RepeatedFieldOfSpanSetCountAction(librarySpans.Spans, 0);
                SpanListPool.Add(librarySpans);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpTrace.InstrumentationLibrarySpans GetSpanListFromPool(string name, string version)
        {
            if (!SpanListPool.TryTake(out var spans))
            {
                spans = new OtlpTrace.InstrumentationLibrarySpans
                {
                    InstrumentationLibrary = new OtlpCommon.InstrumentationLibrary
                    {
                        Name = name, // Name is enforced to not be null, but it can be empty.
                        Version = version ?? string.Empty, // NRE throw by proto
                    },
                };
            }
            else
            {
                spans.InstrumentationLibrary.Name = name;
                spans.InstrumentationLibrary.Version = version ?? string.Empty;
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

            TagEnumerationState otlpTags = default;
            activity.EnumerateTags(ref otlpTags);

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

            if (otlpTags.Created)
            {
                otlpSpan.Attributes.AddRange(otlpTags.Tags);
                otlpTags.Tags.Return();
            }

            otlpSpan.Status = ToOtlpStatus(ref otlpTags);

            EventEnumerationState otlpEvents = default;
            activity.EnumerateEvents(ref otlpEvents);
            if (otlpEvents.Created)
            {
                otlpSpan.Events.AddRange(otlpEvents.Events);
                otlpEvents.Events.Return();
            }

            LinkEnumerationState otlpLinks = default;
            activity.EnumerateLinks(ref otlpLinks);
            if (otlpLinks.Created)
            {
                otlpSpan.Links.AddRange(otlpLinks.Links);
                otlpLinks.Links.Return();
            }

            // Activity does not limit number of attributes, events, links, etc so drop counts are always zero.

            return otlpSpan;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpCommon.KeyValue ToOtlpAttribute(this KeyValuePair<string, object> kvp)
        {
            if (kvp.Value == null)
            {
                return null;
            }

            var attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };

            switch (kvp.Value)
            {
                case string s:
                    attrib.Value.StringValue = s;
                    break;
                case bool b:
                    attrib.Value.BoolValue = b;
                    break;
                case int i:
                    attrib.Value.IntValue = i;
                    break;
                case long l:
                    attrib.Value.IntValue = l;
                    break;
                case double d:
                    attrib.Value.DoubleValue = d;
                    break;
                default:
                    attrib.Value.StringValue = kvp.Value.ToString();
                    break;
            }

            return attrib;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpTrace.Status ToOtlpStatus(ref TagEnumerationState otlpTags)
        {
            var status = StatusHelper.GetStatusCodeForTagValue(otlpTags.StatusCode);

            if (!status.HasValue)
            {
                return null;
            }

            var otlpStatus = new OtlpTrace.Status
            {
                // The numerical values of the two enumerations match, a simple cast is enough.
                Code = (OtlpTrace.Status.Types.StatusCode)(int)status,
            };

            if (otlpStatus.Code != OtlpTrace.Status.Types.StatusCode.Error)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                otlpStatus.DeprecatedCode = OtlpTrace.Status.Types.DeprecatedStatusCode.Ok;
#pragma warning restore CS0612 // Type or member is obsolete
            }
            else
            {
#pragma warning disable CS0612 // Type or member is obsolete
                otlpStatus.DeprecatedCode = OtlpTrace.Status.Types.DeprecatedStatusCode.UnknownError;
#pragma warning restore CS0612 // Type or member is obsolete
            }

            if (!string.IsNullOrEmpty(otlpTags.StatusDescription))
            {
                otlpStatus.Message = otlpTags.StatusDescription;
            }

            return otlpStatus;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpTrace.Span.Types.Link ToOtlpLink(ActivityLink activityLink)
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

            TagEnumerationState otlpTags = default;
            activityLink.EnumerateTags(ref otlpTags);
            if (otlpTags.Created)
            {
                otlpLink.Attributes.AddRange(otlpTags.Tags);
                otlpTags.Tags.Return();
            }

            return otlpLink;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpTrace.Span.Types.Event ToOtlpEvent(ActivityEvent activityEvent)
        {
            var otlpEvent = new OtlpTrace.Span.Types.Event
            {
                Name = activityEvent.Name,
                TimeUnixNano = (ulong)activityEvent.Timestamp.ToUnixTimeNanoseconds(),
            };

            TagEnumerationState otlpTags = default;
            activityEvent.EnumerateTags(ref otlpTags);
            if (otlpTags.Created)
            {
                otlpEvent.Attributes.AddRange(otlpTags.Tags);
                otlpTags.Tags.Return();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpCommon.KeyValue CreateOtlpKeyValue(string key, OtlpCommon.AnyValue value)
        {
            return new OtlpCommon.KeyValue { Key = key, Value = value };
        }

        private struct TagEnumerationState : IActivityEnumerator<KeyValuePair<string, object>>, PeerServiceResolver.IPeerServiceState
        {
            public bool Created;

            public PooledList<OtlpCommon.KeyValue> Tags;

            public string StatusCode;

            public string StatusDescription;

            public string PeerService { get; set; }

            public int? PeerServicePriority { get; set; }

            public string HostName { get; set; }

            public string IpAddress { get; set; }

            public long Port { get; set; }

            public bool ForEach(KeyValuePair<string, object> activityTag)
            {
                if (activityTag.Value == null)
                {
                    return true;
                }

                var key = activityTag.Key;

                switch (key)
                {
                    case SpanAttributeConstants.StatusCodeKey:
                        this.StatusCode = activityTag.Value as string;
                        return true;
                    case SpanAttributeConstants.StatusDescriptionKey:
                        this.StatusDescription = activityTag.Value as string;
                        return true;
                }

                if (!this.Created)
                {
                    this.Tags = PooledList<OtlpCommon.KeyValue>.Create();
                    this.Created = true;
                }

                switch (activityTag.Value)
                {
                    case string s:
                        PeerServiceResolver.InspectTag(ref this, key, s);
                        PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { StringValue = s }));
                        break;
                    case bool b:
                        PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { BoolValue = b }));
                        break;
                    case int i:
                        PeerServiceResolver.InspectTag(ref this, key, i);
                        PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { IntValue = i }));
                        break;
                    case long l:
                        PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { IntValue = l }));
                        break;
                    case double d:
                        PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { DoubleValue = d }));
                        break;
                    case int[] intArray:
                        foreach (var item in intArray)
                        {
                            PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { IntValue = item }));
                        }

                        break;
                    case double[] doubleArray:
                        foreach (var item in doubleArray)
                        {
                            PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { DoubleValue = item }));
                        }

                        break;
                    case bool[] boolArray:
                        foreach (var item in boolArray)
                        {
                            PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { BoolValue = item }));
                        }

                        break;
                    case string[] stringArray:
                        foreach (var item in stringArray)
                        {
                            PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, item == null ? null : new OtlpCommon.AnyValue { StringValue = item }));
                        }

                        break;
                    case long[] longArray:
                        foreach (var item in longArray)
                        {
                            PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { IntValue = item }));
                        }

                        break;
                    default:
                        PooledList<OtlpCommon.KeyValue>.Add(ref this.Tags, CreateOtlpKeyValue(key, new OtlpCommon.AnyValue { StringValue = activityTag.Value.ToString() }));
                        break;
                }

                return true;
            }
        }

        private struct EventEnumerationState : IActivityEnumerator<ActivityEvent>
        {
            public bool Created;

            public PooledList<OtlpTrace.Span.Types.Event> Events;

            public bool ForEach(ActivityEvent activityEvent)
            {
                if (!this.Created)
                {
                    this.Events = PooledList<OtlpTrace.Span.Types.Event>.Create();
                    this.Created = true;
                }

                PooledList<OtlpTrace.Span.Types.Event>.Add(ref this.Events, ToOtlpEvent(activityEvent));

                return true;
            }
        }

        private struct LinkEnumerationState : IActivityEnumerator<ActivityLink>
        {
            public bool Created;

            public PooledList<OtlpTrace.Span.Types.Link> Links;

            public bool ForEach(ActivityLink activityLink)
            {
                if (!this.Created)
                {
                    this.Links = PooledList<OtlpTrace.Span.Types.Link>.Create();
                    this.Created = true;
                }

                PooledList<OtlpTrace.Span.Types.Link>.Add(ref this.Links, ToOtlpLink(activityLink));

                return true;
            }
        }
    }
}
