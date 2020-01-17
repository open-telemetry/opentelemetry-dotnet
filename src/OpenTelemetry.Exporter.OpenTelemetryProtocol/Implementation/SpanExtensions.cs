﻿// <copyright file="SpanExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;

    using OpenTelemetry.Trace;
    using Proto = Opentelemetry.Proto;

    internal static class SpanExtensions
    {
        internal static Proto.Trace.V1.Span ToProtoSpan(this SpanData otelSpan)
        {
            try
            {
                // protobuf doesn't understand Span<T> yet: https://github.com/protocolbuffers/protobuf/issues/3431
                Span<byte> traceIdBytes = stackalloc byte[16];
                Span<byte> spanIdBytes = stackalloc byte[8];

                otelSpan.Context.TraceId.CopyTo(traceIdBytes);
                otelSpan.Context.SpanId.CopyTo(spanIdBytes);

                var parentSpanIdString = ByteString.Empty;
                if (otelSpan.ParentSpanId != default)
                {
                    Span<byte> parentSpanIdBytes = stackalloc byte[8];
                    otelSpan.ParentSpanId.CopyTo(parentSpanIdBytes);
                    parentSpanIdString = ByteString.CopyFrom(parentSpanIdBytes.ToArray());
                }

                return new Proto.Trace.V1.Span
                {
                    Name = otelSpan.Name,

                    // TODO: Utilize new Span.Types.SpanKind below when updated protos are incorporated, also confirm default for SpanKind.Internal
                    Kind = otelSpan.Kind == SpanKind.Client || otelSpan.Kind == SpanKind.Producer ?
                        Proto.Trace.V1.Span.Types.SpanKind.Client :
                        Proto.Trace.V1.Span.Types.SpanKind.Server,

                    TraceId = ByteString.CopyFrom(traceIdBytes.ToArray()),
                    SpanId = ByteString.CopyFrom(spanIdBytes.ToArray()),
                    ParentSpanId = parentSpanIdString,

                    StartTime = otelSpan.StartTimestamp.ToTimestamp(),
                    EndTime = otelSpan.EndTimestamp.ToTimestamp(),
                    Status = !otelSpan.Status.IsValid
                        ? null
                        : new Opentelemetry.Proto.Trace.V1.Status
                        {
                            Code = (int)otelSpan.Status.CanonicalCode,
                            Message = otelSpan.Status.Description ?? string.Empty,
                        },
                    ChildSpanCount = null,
                    Attributes = FromAttributes(otelSpan.Attributes),
                    TimeEvents = FromITimeEvents(otelSpan.Events),
                    Links = new Proto.Trace.V1.Span.Types.Links
                    {
                        Link = { otelSpan.Links.Select(FromILink), },
                    },
                };
            }
            catch (Exception e)
            {
                // TODO: Is there a way to handle this better?
                // This type of error processing is very aggressive and doesn't follow the
                // error handling practices when smart defaults should be used when possible.
                // See: https://github.com/open-telemetry/OpenTelemetry-dotnet/blob/master/docs/error-handling.md
                ExporterEventSource.Log.FailedToConvertToProtoDefinitionError(e);
            }

            return null;
        }

        private static Proto.Trace.V1.Span.Types.Attributes FromAttributes(IEnumerable<KeyValuePair<string, object>> source)
        {
            var attributes = new Opentelemetry.Proto.Trace.V1.Span.Types.Attributes();

            attributes.AttributeMap.Add(source.ToDictionary(
                kvp => kvp.Key,
                kvp => FromAttribute(kvp.Value)));

            return attributes;
        }

        private static Proto.Trace.V1.AttributeValue FromAttribute(object value)
        {
            switch (value)
            {
                case string s:
                    return new Proto.Trace.V1.AttributeValue { StringValue = s };
                case bool b:
                    return new Proto.Trace.V1.AttributeValue { BoolValue = b };
                case long l:
                    return new Proto.Trace.V1.AttributeValue { IntValue = l };
                case double d:
                    return new Proto.Trace.V1.AttributeValue { DoubleValue = d };
                default:
                    return new Proto.Trace.V1.AttributeValue
                    {
                        StringValue = value?.ToString(),
                    };
            }
        }

        private static Proto.Trace.V1.Span.Types.TimedEvents FromITimeEvents(IEnumerable<Event> events)
        {
            var eventArray = events as Event[] ?? events.ToArray();
            var timedEvents = new Proto.Trace.V1.Span.Types.TimedEvents
            {
                TimedEvent = { eventArray.Select(FromITimeEvent), },
            };

            timedEvents.TimedEvent.AddRange(eventArray.Select(FromITimeEvent));

            return timedEvents;
        }

        private static Proto.Trace.V1.Span.Types.Link FromILink(Link source)
        {
            // protobuf doesn't understand Span<T> yet: https://github.com/protocolbuffers/protobuf/issues/3431
            Span<byte> traceIdBytes = stackalloc byte[16];
            Span<byte> spanIdBytes = stackalloc byte[8];

            source.Context.TraceId.CopyTo(traceIdBytes);
            source.Context.SpanId.CopyTo(spanIdBytes);

            var result = new Proto.Trace.V1.Span.Types.Link
            {
                Attributes = FromIAttributeMap(source.Attributes),
                TraceId = ByteString.CopyFrom(traceIdBytes.ToArray()),
                SpanId = ByteString.CopyFrom(spanIdBytes.ToArray()),
            };

            return result;
        }

        private static Proto.Trace.V1.Span.Types.TimedEvent FromITimeEvent(Event source)
        {
            return new Proto.Trace.V1.Span.Types.TimedEvent
            {
                Time = source.Timestamp.ToTimestamp(),
                Event = new Proto.Trace.V1.Span.Types.TimedEvent.Types.Event
                {
                    Name = source.Name,
                    Attributes = FromIAttributeMap(source.Attributes),
                },
            };
        }

        private static Proto.Trace.V1.Span.Types.Attributes FromIAttributeMap(IEnumerable<KeyValuePair<string, object>> source)
        {
            var attributes = new Proto.Trace.V1.Span.Types.Attributes();

            attributes.AttributeMap.Add(source.ToDictionary(
                kvp => kvp.Key,
                kvp => FromAttribute(kvp.Value)));

            return attributes;
        }
    }
}
