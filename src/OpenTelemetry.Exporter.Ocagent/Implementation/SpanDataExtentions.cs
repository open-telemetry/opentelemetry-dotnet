// <copyright file="SpanDataExtentions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Ocagent.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;

    using OpenTelemetry.Proto.Trace.V1;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    internal static class SpanDataExtentions
    {
        internal static Span ToProtoSpan(this ISpanData spanData)
        {
            try
            {
                return new Span
                {
                    Name = new TruncatableString { Value = spanData.Name },

                    // TODO: Utilize new Span.Types.SpanKind below when updated protos are incorporated
                    Kind = spanData.Kind == SpanKind.Client || spanData.Kind == SpanKind.Producer ? Span.Types.SpanKind.Client : Span.Types.SpanKind.Server,

                    TraceId = ByteString.CopyFrom(spanData.Context.TraceId.Bytes),
                    SpanId = ByteString.CopyFrom(spanData.Context.SpanId.Bytes),
                    ParentSpanId =
                        ByteString.CopyFrom(spanData.ParentSpanId?.Bytes ?? new byte[0]),

                    StartTime = new Timestamp
                    {
                        Nanos = spanData.StartTimestamp.Nanos,
                        Seconds = spanData.StartTimestamp.Seconds,
                    },
                    EndTime = new Timestamp
                    {
                        Nanos = spanData.EndTimestamp.Nanos,
                        Seconds = spanData.EndTimestamp.Seconds,
                    },
                    Status = spanData.Status == null
                        ? null
                        : new OpenTelemetry.Proto.Trace.V1.Status
                        {
                            Code = (int)spanData.Status.CanonicalCode,
                            Message = spanData.Status.Description ?? string.Empty,
                        },
                    SameProcessAsParentSpan =
                        !spanData.HasRemoteParent.GetValueOrDefault() && spanData.ParentSpanId != null,
                    ChildSpanCount = spanData.ChildSpanCount.HasValue ? (uint)spanData.ChildSpanCount.Value : 0,
                    Attributes = FromIAttributes(spanData.Attributes),
                    TimeEvents = FromITimeEvents(spanData.Events),
                    Links = new Span.Types.Links
                    {
                        DroppedLinksCount = spanData.Links.DroppedLinksCount,
                        Link = { spanData.Links.Links.Select(FromILink), },
                    },
                };
            }
            catch (Exception e)
            {
                // TODO: Is there a way to handle this better?
                // This type of error processing is very aggressive and doesn't follow the
                // error handling practices when smart defaults should be used when possible.
                // See: https://github.com/open-telemetry/OpenTelemetry-dotnet/blob/master/docs/error-handling.md
                ExporterOcagentEventSource.Log.FailedToConvertToProtoDefinitionError(e);
            }

            return null;
        }

        private static Span.Types.Attributes FromIAttributes(IAttributes source)
        {
            var attributes = new Span.Types.Attributes
            {
                DroppedAttributesCount = source.DroppedAttributesCount,
            };

            attributes.AttributeMap.Add(source.AttributeMap.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Match(
                    s => new OpenTelemetry.Proto.Trace.V1.AttributeValue { StringValue = new TruncatableString() { Value = s } },
                    b => new OpenTelemetry.Proto.Trace.V1.AttributeValue { BoolValue = b },
                    l => new OpenTelemetry.Proto.Trace.V1.AttributeValue { IntValue = l },
                    d => new OpenTelemetry.Proto.Trace.V1.AttributeValue { DoubleValue = d },
                    o => new OpenTelemetry.Proto.Trace.V1.AttributeValue { StringValue = new TruncatableString() { Value = o?.ToString() } })));

            return attributes;
        }

        private static Span.Types.TimeEvents FromITimeEvents(ITimedEvents<IEvent> events)
        {
            var timedEvents = new Span.Types.TimeEvents
            {
                DroppedAnnotationsCount = events.DroppedEventsCount,
                TimeEvent = { events.Events.Select(FromITimeEvent), },
            };

            timedEvents.TimeEvent.AddRange(events.Events.Select(FromITimeEvent));

            return timedEvents;
        }

        private static Span.Types.Link FromILink(ILink source)
        {
            return new Span.Types.Link
            {
                TraceId = ByteString.CopyFrom(source.TraceId.Bytes),
                SpanId = ByteString.CopyFrom(source.SpanId.Bytes),
                Type = source.Type == LinkType.ChildLinkedSpan ? Span.Types.Link.Types.Type.ChildLinkedSpan : Span.Types.Link.Types.Type.ParentLinkedSpan,
                Attributes = FromIAttributeMap(source.Attributes),
            };
        }

        private static Span.Types.TimeEvent FromITimeEvent(ITimedEvent<IEvent> source)
        {
            return new Span.Types.TimeEvent
            {
                Time = new Timestamp
                {
                    Nanos = source.Timestamp.Nanos,
                    Seconds = source.Timestamp.Seconds,
                },
                Annotation = new Span.Types.TimeEvent.Types.Annotation
                {
                    Description = new TruncatableString { Value = source.Event.Name },
                    Attributes = FromIAttributeMap(source.Event.Attributes),
                },
            };
        }

        private static Span.Types.Attributes FromIAttributeMap(IDictionary<string, IAttributeValue> source)
        {
            var attributes = new Span.Types.Attributes();

            attributes.AttributeMap.Add(source.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Match(
                    s => new OpenTelemetry.Proto.Trace.V1.AttributeValue { StringValue = new TruncatableString() { Value = s } },
                    b => new OpenTelemetry.Proto.Trace.V1.AttributeValue { BoolValue = b },
                    l => new OpenTelemetry.Proto.Trace.V1.AttributeValue { IntValue = l },
                    d => new OpenTelemetry.Proto.Trace.V1.AttributeValue { DoubleValue = d },
                    o => new OpenTelemetry.Proto.Trace.V1.AttributeValue { StringValue = new TruncatableString() { Value = o?.ToString() } })));

            return attributes;
        }
    }
}
