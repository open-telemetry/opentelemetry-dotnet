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
using OpenTelemetry.Trace;
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
                    resource.Key.Attributes.Select(ToOtlpAttribute).Where(a => a != null));

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

                Status = ToOtlpStatus(activity.GetStatus()),

                StartTimeUnixNano = (ulong)startTimeUnixNano,
                EndTimeUnixNano = (ulong)(startTimeUnixNano + activity.Duration.ToNanoseconds()),
            };

            foreach (var kvp in activity.TagObjects)
            {
                var attributes = ToOtlpAttributes(kvp);
                if (attributes != null
                    && attributes.All(a => a.Key != SpanAttributeConstants.StatusCodeKey && a.Key != SpanAttributeConstants.StatusDescriptionKey))
                {
                    otlpSpan.Attributes.AddRange(attributes);
                }
            }

            otlpSpan.Events.AddRange(activity.Events.Select(ToOtlpEvent));
            otlpSpan.Links.AddRange(activity.Links.Select(ToOtlpLink));

            // Activity does not limit number of attributes, events, links, etc so drop counts are always zero.

            return otlpSpan;
        }

        private static OtlpTrace.Status ToOtlpStatus(Status status)
        {
            if (!status.IsValid)
            {
                return null;
            }

            var otlpStatus = new Opentelemetry.Proto.Trace.V1.Status
            {
                // The numerical values of the two enumerations match, a simple cast is enough.
                Code = (OtlpTrace.Status.Types.StatusCode)status.CanonicalCode,
            };

            if (!string.IsNullOrEmpty(status.Description))
            {
                otlpStatus.Message = status.Description;
            }

            return otlpStatus;
        }

        private static Dictionary<Resource, Dictionary<ActivitySource, List<OtlpTrace.Span>>> GroupByResourceAndLibrary(
            IEnumerable<Activity> activityBatch)
        {
            var result = new Dictionary<Resource, Dictionary<ActivitySource, List<OtlpTrace.Span>>>();
            foreach (var activity in activityBatch)
            {
                var protoSpan = activity.ToOtlpSpan();
                if (protoSpan == null)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateActivity(
                        nameof(ActivityExtensions),
                        nameof(GroupByResourceAndLibrary));
                    continue;
                }

                var resource = activity.GetResource();
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

            if (activityLink.Tags != null)
            {
                foreach (var attribute in from tag in activityLink.Tags
                                          let attribute = ToOtlpAttributes(tag)
                                          where attribute != null && attribute.Any()
                                          select attribute)
                {
                    otlpLink.Attributes.AddRange(attribute);
                }
            }

            return otlpLink;
        }

        private static OtlpTrace.Span.Types.Event ToOtlpEvent(ActivityEvent activityEvent)
        {
            var otlpEvent = new OtlpTrace.Span.Types.Event
            {
                Name = activityEvent.Name,
                TimeUnixNano = (ulong)activityEvent.Timestamp.ToUnixTimeNanoseconds(),
            };

            foreach (var attribute in from tag in activityEvent.Tags
                                      let attribute = ToOtlpAttributes(tag)
                                      where attribute != null && attribute.Any()
                                      select attribute)
            {
                otlpEvent.Attributes.AddRange(attribute);
            }

            return otlpEvent;
        }

        private static OtlpCommon.KeyValue ToOtlpAttribute(KeyValuePair<string, object> kvp)
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

        private static List<OtlpCommon.KeyValue> ToOtlpAttributes(KeyValuePair<string, object> kvp)
        {
            if (kvp.Value == null)
            {
                return null;
            }

            var attributes = new List<OtlpCommon.KeyValue>();
            var attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };
            switch (kvp.Value)
            {
                case string s:
                    attrib.Value.StringValue = s;
                    attributes.Add(attrib);
                    break;
                case bool b:
                    attrib.Value.BoolValue = b;
                    attributes.Add(attrib);
                    break;
                case int i:
                    attrib.Value.IntValue = i;
                    attributes.Add(attrib);
                    break;
                case long l:
                    attrib.Value.IntValue = l;
                    attributes.Add(attrib);
                    break;
                case double d:
                    attrib.Value.DoubleValue = d;
                    attributes.Add(attrib);
                    break;
                case int[] intArray:
                    foreach (var item in intArray)
                    {
                        attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };
                        attrib.Value.IntValue = item;
                        attributes.Add(attrib);
                    }

                    break;
                case double[] doubleArray:
                    foreach (var item in doubleArray)
                    {
                        attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };
                        attrib.Value.DoubleValue = item;
                        attributes.Add(attrib);
                    }

                    break;
                case bool[] boolArray:
                    foreach (var item in boolArray)
                    {
                        attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };
                        attrib.Value.BoolValue = item;
                        attributes.Add(attrib);
                    }

                    break;
                case string[] stringArray:
                    foreach (var item in stringArray)
                    {
                        attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };
                        attrib.Value.StringValue = item;
                        attributes.Add(attrib);
                    }

                    break;
                default:
                    attrib.Value.StringValue = kvp.Value.ToString();
                    attributes.Add(attrib);
                    break;
            }

            return attributes;
        }
    }
}
