// <copyright file="SpanDataExtensions.cs" company="OpenTelemetry Authors">
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
using System.Linq;

using Google.Protobuf;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    /// <summary>
    /// Extensions to convert <see cref="SpanData"/> into the corresponding OpenTelemetry Protocol (OTLP)
    /// data structures.
    /// </summary>
    internal static class SpanDataExtensions
    {
        private static readonly IEnumerable<KeyValuePair<string, object>> EmptySpanDataAttributes = Enumerable.Empty<KeyValuePair<string, object>>();
        private static readonly IEnumerable<Event> EmptySpanDataEvents = Enumerable.Empty<Event>();
        private static readonly IEnumerable<Link> EmptySpanDataLinks = Enumerable.Empty<Link>();

        internal static IEnumerable<OtlpTrace.ResourceSpans> ToOtlpResourceSpans(this IEnumerable<SpanData> spanDataList)
        {
            var resourceToLibraryAndSpans = GroupByResourceAndLibrary(spanDataList);
            var resourceSpansList = new List<OtlpTrace.ResourceSpans>(resourceToLibraryAndSpans.Count);

            foreach (var resource in resourceToLibraryAndSpans)
            {
                // TODO: this is a temporary workaround since library is still on the resource, not on its own field.
                var libName = resource.Key.Attributes.FirstOrDefault(
                    kvp => kvp.Key == Resource.LibraryNameKey);
                var libVersion = resource.Key.Attributes.FirstOrDefault(
                    kvp => kvp.Key == Resource.LibraryVersionKey);

                var libraryList = new List<OtlpTrace.InstrumentationLibrarySpans>(resource.Value.Count);
                foreach (var library in resource.Value)
                {
                    var otlpLibrarySpans = new OtlpTrace.InstrumentationLibrarySpans
                    {
                        InstrumentationLibrary = new OtlpCommon.InstrumentationLibrary
                        {
                            Name = libName.Value?.ToString() ?? string.Empty,
                            Version = libVersion.Value?.ToString() ?? string.Empty,
                        },
                    };

                    otlpLibrarySpans.Spans.AddRange(library.Value);
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

        internal static OtlpTrace.Span ToOtlpSpan(this SpanData spanData)
        {
            // protobuf doesn't understand Span<T> yet: https://github.com/protocolbuffers/protobuf/issues/3431
            Span<byte> traceIdBytes = stackalloc byte[16];
            Span<byte> spanIdBytes = stackalloc byte[8];

            spanData.Context.TraceId.CopyTo(traceIdBytes);
            spanData.Context.SpanId.CopyTo(spanIdBytes);

            var parentSpanIdString = ByteString.Empty;
            if (spanData.ParentSpanId != default)
            {
                Span<byte> parentSpanIdBytes = stackalloc byte[8];
                spanData.ParentSpanId.CopyTo(parentSpanIdBytes);
                parentSpanIdString = ByteString.CopyFrom(parentSpanIdBytes.ToArray());
            }

            var otlpSpan = new OtlpTrace.Span
            {
                Name = spanData.Name,

                Kind = spanData.Kind == null
                    ? OtlpTrace.Span.Types.SpanKind.Unspecified
                    : (OtlpTrace.Span.Types.SpanKind)spanData.Kind.Value,

                TraceId = ByteString.CopyFrom(traceIdBytes.ToArray()),
                SpanId = ByteString.CopyFrom(spanIdBytes.ToArray()),
                ParentSpanId = parentSpanIdString,

                Status = ToOtlpStatus(spanData.Status),

                StartTimeUnixNano = (ulong)spanData.StartTimestamp.ToUnixTimeNanoseconds(),
                EndTimeUnixNano = (ulong)spanData.EndTimestamp.ToUnixTimeNanoseconds(),
            };

            if (!ReferenceEquals(spanData.Attributes, EmptySpanDataAttributes))
            {
                otlpSpan.Attributes.AddRange(spanData.Attributes.Select(ToOtlpAttribute));

                // TODO: get dropped count.
            }

            if (!ReferenceEquals(spanData.Events, EmptySpanDataEvents))
            {
                otlpSpan.Events.AddRange(spanData.Events.Select(ToOtlpEvent));

                // TODO: get dropped count.
            }

            if (!ReferenceEquals(spanData.Links, EmptySpanDataLinks))
            {
                otlpSpan.Links.AddRange(spanData.Links.Select(ToOtlpLink));

                // TODO: get dropped count.
            }

            return otlpSpan;
        }

        private static OtlpTrace.Status ToOtlpStatus(Status status)
        {
            // At this stage Status.IsValid is always true, just add status message if !Ok
            if (status == Status.Ok)
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

        private static Dictionary<Resource, Dictionary<Resource, List<OtlpTrace.Span>>> GroupByResourceAndLibrary(
            IEnumerable<SpanData> spanDataList)
        {
            var result = new Dictionary<Resource, Dictionary<Resource, List<OtlpTrace.Span>>>();
            foreach (var spanData in spanDataList)
            {
                var protoSpan = spanData.ToOtlpSpan();

                // TODO: SpanData will be updated to have Resource besides library resource.
                var resource = spanData.LibraryResource;
                if (!result.TryGetValue(resource, out var libraryToSpans))
                {
                    libraryToSpans = new Dictionary<Resource, List<OtlpTrace.Span>>();
                    result[resource] = libraryToSpans;
                }

                // TODO: for now library info is in the Resources,
                var library = spanData.LibraryResource;
                if (!libraryToSpans.TryGetValue(library, out var spans))
                {
                    spans = new List<OtlpTrace.Span>();
                    libraryToSpans[library] = spans;
                }

                spans.Add(protoSpan);
            }

            return result;
        }

        private static OtlpCommon.KeyValue ToOtlpAttribute(KeyValuePair<string, object> kvp)
        {
            switch (kvp.Value)
            {
                case string s:
                    return new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { StringValue = s } };
                case bool b:
                    return new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { BoolValue = b } };
                case int i:
                    return new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { IntValue = i } };
                case long l:
                    return new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { IntValue = l } };
                case double d:
                    return new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { DoubleValue = d } };
                default:
                    return new OtlpCommon.KeyValue
                    {
                        Key = kvp.Key,
                        Value = new OtlpCommon.AnyValue { StringValue = kvp.Value == null ? string.Empty : kvp.Value.ToString() },
                    };
            }
        }

        private static OtlpTrace.Span.Types.Link ToOtlpLink(Link source)
        {
            // protobuf doesn't understand Span<T> yet: https://github.com/protocolbuffers/protobuf/issues/3431
            Span<byte> traceIdBytes = stackalloc byte[16];
            Span<byte> spanIdBytes = stackalloc byte[8];

            source.Context.TraceId.CopyTo(traceIdBytes);
            source.Context.SpanId.CopyTo(spanIdBytes);

            var otlpLink = new OtlpTrace.Span.Types.Link
            {
                TraceId = ByteString.CopyFrom(traceIdBytes.ToArray()),
                SpanId = ByteString.CopyFrom(spanIdBytes.ToArray()),
            };

            otlpLink.Attributes.AddRange(source.Attributes.Select(ToOtlpAttribute));

            return otlpLink;
        }

        private static OtlpTrace.Span.Types.Event ToOtlpEvent(Event source)
        {
            var otlpEvent = new OtlpTrace.Span.Types.Event
            {
                Name = source.Name,
                TimeUnixNano = (ulong)source.Timestamp.ToUnixTimeNanoseconds(),
            };

            if (!ReferenceEquals(source.Attributes, EmptySpanDataAttributes))
            {
                otlpEvent.Attributes.AddRange(source.Attributes.Select(ToOtlpAttribute));
            }

            return otlpEvent;
        }
    }
}
