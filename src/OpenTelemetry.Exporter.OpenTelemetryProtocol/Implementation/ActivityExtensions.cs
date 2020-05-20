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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Google.Protobuf;

using OpenTelemetry.Resources;

using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class ActivityExtensions
    {
        internal static IEnumerable<OtlpTrace.ResourceSpans> ToOtlpResourceSpans(this IEnumerable<Activity> activityBatch)
        {
            var resourceToLibraryAndSpans = GroupByResourceAndLibrary(activityBatch);
            var resourceSpansList = new List<OtlpTrace.ResourceSpans>(resourceToLibraryAndSpans.Count);

            foreach (var resource in resourceToLibraryAndSpans)
            {
                var libraryList = new List<OtlpTrace.InstrumentationLibrarySpans>(resource.Value.Count);
                foreach (var activitySourceEntry in resource.Value)
                {
                    var activitySource = activitySourceEntry.Key;
                    var otlpLibrarySpans = new OtlpTrace.InstrumentationLibrarySpans
                    {
                        InstrumentationLibrary = new OtlpCommon.InstrumentationLibrary
                        {
                            Name = activitySource.Name, // Name is enforced to not be null, but it can be empty.
                            Version = activitySource.Version ?? string.Empty, // NRE throw by proto
                        },
                    };

                    otlpLibrarySpans.Spans.AddRange(activitySourceEntry.Value);
                    libraryList.Add(otlpLibrarySpans);
                }

                var otlpResources = new OtlpResource.Resource();
                otlpResources.Attributes.AddRange(
                    resource.Key.Attributes.Select(ToOtlpAttribute));

                var otlpResourceSpans = new OtlpTrace.ResourceSpans
                {
                    Resource = otlpResources,
                };
                otlpResourceSpans.InstrumentationLibrarySpans.AddRange(libraryList);

                resourceSpansList.Add(otlpResourceSpans);
            }

            return resourceSpansList;
        }

        internal static OtlpTrace.Span ToOtlpSpan(this Activity activity)
        {
            if (activity.IdFormat != ActivityIdFormat.W3C)
            {
                // Only ActivityIdFormat.W3C is supported, in principle this should never be
                // hit under the OpenTelemetry SDK.
                return null;
            }

            // protobuf doesn't understand Span<T> yet: https://github.com/protocolbuffers/protobuf/issues/3431
            Span<byte> traceIdBytes = stackalloc byte[16];
            Span<byte> spanIdBytes = stackalloc byte[8];

            activity.TraceId.CopyTo(traceIdBytes);
            activity.SpanId.CopyTo(spanIdBytes);

            var parentSpanIdString = ByteString.Empty;
            if (activity.ParentSpanId != default)
            {
                Span<byte> parentSpanIdBytes = stackalloc byte[8];
                activity.ParentSpanId.CopyTo(parentSpanIdBytes);
                parentSpanIdString = ByteString.CopyFrom(parentSpanIdBytes.ToArray());
            }

            var startTimeUnixNano = activity.StartTimeUtc.ToUnixTimeNanoseconds();
            var otlpSpan = new OtlpTrace.Span
            {
                Name = activity.DisplayName,

                Kind = (OtlpTrace.Span.Types.SpanKind)(activity.Kind + 1), // TODO: there is an offset of 1 on the enum.

                TraceId = ByteString.CopyFrom(traceIdBytes.ToArray()),
                SpanId = ByteString.CopyFrom(spanIdBytes.ToArray()),
                ParentSpanId = parentSpanIdString,

                // TODO: Status is still pending, need to pursue OTEL spec change.

                StartTimeUnixNano = (ulong)startTimeUnixNano,
                EndTimeUnixNano = (ulong)(startTimeUnixNano + activity.Duration.ToNanoseconds()),
            };

            foreach (var kvp in activity.Tags)
            {
                // TODO: attempt to convert to supported types?
                // TODO: enforce no duplicate keys?
                // TODO: drop if Value is null?
                // TODO: reverse?
                var attrib = new OtlpCommon.AttributeKeyValue { Key = kvp.Key, StringValue = kvp.Value };
                otlpSpan.Attributes.Add(attrib);
            }

            otlpSpan.Events.AddRange(activity.Events.Select(ToOtlpEvent));
            otlpSpan.Links.AddRange(activity.Links.Select(ToOtlpLink));

            // Activity does not limit number of attributes, events, links, etc so drop counts are always zero.

            return otlpSpan;
        }

        private static Dictionary<Resource, Dictionary<ActivitySource, List<OtlpTrace.Span>>> GroupByResourceAndLibrary(
            IEnumerable<Activity> activityBatch)
        {
            // TODO: there is no Resource associated here, other SDKs associate it to the span, that's not an option here.
            var fakeResource = Resource.Empty;

            var result = new Dictionary<Resource, Dictionary<ActivitySource, List<OtlpTrace.Span>>>();
            foreach (var activity in activityBatch)
            {
                var protoSpan = activity.ToOtlpSpan();
                if (protoSpan == null)
                {
                    // If it could not be translated ignore it.
                    // TODO: report this issue.
                    continue;
                }

                // TODO: Retrieve resources for trace (not library/ActivitySource)
                var resource = fakeResource;
                if (!result.TryGetValue(resource, out var libraryToSpans))
                {
                    libraryToSpans = new Dictionary<ActivitySource, List<OtlpTrace.Span>>();
                    result[resource] = libraryToSpans;
                }

                // The ActivitySource is the equivalent of OpenTelemetry LibraryResource.
                var activitySource = activity.Source;
                if (!libraryToSpans.TryGetValue(activitySource, out var spans))
                {
                    spans = new List<OtlpTrace.Span>();
                    libraryToSpans[activitySource] = spans;
                }

                spans.Add(protoSpan);
            }

            return result;
        }

        private static OtlpTrace.Span.Types.Link ToOtlpLink(ActivityLink activityLink)
        {
            // protobuf doesn't understand Span<T> yet: https://github.com/protocolbuffers/protobuf/issues/3431
            Span<byte> traceIdBytes = stackalloc byte[16];
            Span<byte> spanIdBytes = stackalloc byte[8];

            activityLink.Context.TraceId.CopyTo(traceIdBytes);
            activityLink.Context.SpanId.CopyTo(spanIdBytes);

            var otlpLink = new OtlpTrace.Span.Types.Link
            {
                TraceId = ByteString.CopyFrom(traceIdBytes.ToArray()),
                SpanId = ByteString.CopyFrom(spanIdBytes.ToArray()),
            };

            otlpLink.Attributes.AddRange(activityLink.Attributes.Select(ToOtlpAttribute));

            return otlpLink;
        }

        private static OtlpTrace.Span.Types.Event ToOtlpEvent(ActivityEvent activityEvent)
        {
            var otlpEvent = new OtlpTrace.Span.Types.Event
            {
                Name = activityEvent.Name,
                TimeUnixNano = (ulong)activityEvent.Timestamp.ToUnixTimeNanoseconds(),
            };

            otlpEvent.Attributes.AddRange(activityEvent.Attributes.Select(ToOtlpAttribute));

            return otlpEvent;
        }

        private static OtlpCommon.AttributeKeyValue ToOtlpAttribute(KeyValuePair<string, object> kvp)
        {
            switch (kvp.Value)
            {
                case string s:
                    return new OtlpCommon.AttributeKeyValue { Key = kvp.Key, StringValue = s };
                case bool b:
                    return new OtlpCommon.AttributeKeyValue { Key = kvp.Key, BoolValue = b };
                case long l:
                    return new OtlpCommon.AttributeKeyValue { Key = kvp.Key, IntValue = l };
                case double d:
                    return new OtlpCommon.AttributeKeyValue { Key = kvp.Key, DoubleValue = d };
                default:
                    return new OtlpCommon.AttributeKeyValue
                    {
                        Key = kvp.Key,
                        StringValue = kvp.Value?.ToString(),
                    };
            }
        }
    }
}
