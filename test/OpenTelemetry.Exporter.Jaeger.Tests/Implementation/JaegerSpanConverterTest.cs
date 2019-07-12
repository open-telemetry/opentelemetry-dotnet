// <copyright file="JaegerSpanConverterTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Exporter.Jaeger.Implimentation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using Xunit;

    public class JaegerSpanConverterTest
    {
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;

        public JaegerSpanConverterTest()
        {
        }

        public static List<object[]> DataProviderBuildTag() => new List<object[]>
        {
            new object[] { AttributeValue.StringAttributeValue("value"), JaegerTagType.STRING, "value" },
            new object[] { AttributeValue.LongAttributeValue(1), JaegerTagType.LONG, (long) 1 },
            new object[] { AttributeValue.DoubleAttributeValue(1), JaegerTagType.DOUBLE, (double) 1 },
            new object[] { AttributeValue.BooleanAttributeValue(true), JaegerTagType.BOOL, true },
        };

        [Theory]
        [MemberData(nameof(DataProviderBuildTag))]
        public void TestBuildTag(IAttributeValue tagValue, JaegerTagType expectedTagType, object expectedValue)
        {
            var telemetryTag = new KeyValuePair<string, IAttributeValue>("key", tagValue);
            var tag = telemetryTag.ToJaegerTag();

            Assert.Equal(expectedTagType, tag.VType);
            Assert.Equal("key", tag.Key);

            switch (expectedTagType)
            {
                case JaegerTagType.BOOL:
                    Assert.Equal(expectedValue, tag.VBool);
                    break;
                case JaegerTagType.LONG:
                    Assert.Equal(expectedValue, tag.VLong);
                    break;
                case JaegerTagType.DOUBLE:
                    Assert.Equal(expectedValue, tag.VDouble);
                    break;
                case JaegerTagType.STRING:
                default:
                    Assert.Equal(expectedValue, tag.VStr);
                    break;
            }
        }

        [Fact]
        public void TestConvertSpan()
        {
            var eventTimestamp = Timestamp.Create(100, 100);

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var attributes = Attributes.Create(new Dictionary<string, IAttributeValue>{
                { "stringKey", AttributeValue.StringAttributeValue("value")},
                { "longKey", AttributeValue.LongAttributeValue(1)},
                { "doubleKey", AttributeValue.DoubleAttributeValue(1)},
                { "boolKey", AttributeValue.BooleanAttributeValue(true)},
            }, 0);
            var events = TimedEvents<IEvent>.Create(new List<ITimedEvent<IEvent>>
            {
                TimedEvent<IEvent>.Create(
                    eventTimestamp,
                    Event.Create(
                        "Event1",
                        new Dictionary<string, IAttributeValue>
                        {
                            {"key", AttributeValue.StringAttributeValue("value") }
                        }
                    )
                ),
                TimedEvent<IEvent>.Create(
                    eventTimestamp,
                    Event.Create(
                        "Event2",
                        new Dictionary<string, IAttributeValue>
                        {
                            {"key", AttributeValue.StringAttributeValue("value") }
                        }
                    )
                )
            }, 0);



            var linkedSpanId = ActivitySpanId.CreateRandom();
            var linkedAttributes = Attributes.Create(new Dictionary<string, IAttributeValue>{
                { "stringKey", AttributeValue.StringAttributeValue("value")},
                { "longKey", AttributeValue.LongAttributeValue(1)},
                { "doubleKey", AttributeValue.DoubleAttributeValue(1)},
                { "boolKey", AttributeValue.BooleanAttributeValue(true)},
            }, 0);

            var links = LinkList.Create(new List<ILink>
            {
                Link.FromSpanContext(SpanContext.Create(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty)),
            }, 0);

            var spanData = SpanData.Create(
                SpanContext.Create(
                    traceId,
                    spanId,
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty
                ),
                parentSpanId,
                Resource.Empty,
                "Name",
                Timestamp.Create(5100, 500),
                attributes,
                events,
                links,
                null,
                Status.Ok,
                SpanKind.Client,
                Timestamp.Create(6100, 500)
            );

            var jaegerSpan = spanData.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count);
            var jaegerLog = jaegerSpan.Logs[0];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count, 2);
            var eventField = jaegerLog.Fields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = jaegerLog.Fields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);


            // NOTE: In Java, the order is different (event, value, key) because the HashMap algorithm is different.
            // thriftLog = jaegerSpan.Logs[1];
            // Assert.Equal(3, thriftLog.Fields.Count);
            // thriftTag = thriftLog.Fields[0];
            // Assert.Equal("event", thriftTag.Key);
            // Assert.Equal("baggage", thriftTag.VStr);
            // thriftTag = thriftLog.Fields[1];
            // Assert.Equal("key", thriftTag.Key);
            // Assert.Equal("foo", thriftTag.VStr);
            // thriftTag = thriftLog.Fields[2];
            // Assert.Equal("value", thriftTag.Key);
            // Assert.Equal("bar", thriftTag.VStr);
        }

        // [Fact]
        // public void TestConvertSpanOneReferenceChildOf()
        // {
        //     Span parent = (Span)_tracer.BuildSpan("foo").Start();

        //     Span child = (Span)_tracer.BuildSpan("foo")
        //         .AsChildOf(parent)
        //         .Start();

        //     ThriftSpan span = JaegerThriftSpanConverter.ConvertSpan(child);

        //     Assert.Equal((long)child.Context.ParentId, span.ParentSpanId);
        //     Assert.Empty(span.References);
        // }

        // [Fact]
        // public void TestConvertSpanTwoReferencesChildOf()
        // {
        //     Span parent = (Span)_tracer.BuildSpan("foo").Start();
        //     Span parent2 = (Span)_tracer.BuildSpan("foo").Start();

        //     Span child = (Span)_tracer.BuildSpan("foo")
        //         .AsChildOf(parent)
        //         .AsChildOf(parent2)
        //         .Start();

        //     ThriftSpan span = JaegerThriftSpanConverter.ConvertSpan(child);

        //     Assert.Equal(0, span.ParentSpanId);
        //     Assert.Equal(2, span.References.Count);
        //     Assert.Equal(BuildReference(parent.Context, References.ChildOf), span.References[0], _thriftReferenceComparer);
        //     Assert.Equal(BuildReference(parent2.Context, References.ChildOf), span.References[1], _thriftReferenceComparer);
        // }

        // [Fact]
        // public void TestConvertSpanMixedReferences()
        // {
        //     Span parent = (Span)_tracer.BuildSpan("foo").Start();
        //     Span parent2 = (Span)_tracer.BuildSpan("foo").Start();

        //     Span child = (Span)_tracer.BuildSpan("foo")
        //         .AddReference(References.FollowsFrom, parent.Context)
        //         .AsChildOf(parent2)
        //         .Start();

        //     ThriftSpan span = JaegerThriftSpanConverter.ConvertSpan(child);

        //     Assert.Equal(0, span.ParentSpanId);
        //     Assert.Equal(2, span.References.Count);
        //     Assert.Equal(BuildReference(parent.Context, References.FollowsFrom), span.References[0], _thriftReferenceComparer);
        //     Assert.Equal(BuildReference(parent2.Context, References.ChildOf), span.References[1], _thriftReferenceComparer);
        // }

        // private static ThriftReference BuildReference(SpanContext context, string referenceType)
        // {
        //     return JaegerThriftSpanConverter.BuildReferences(new List<Reference> { new Reference(context, referenceType) }.AsReadOnly())[0];
        // }

        // private class ThriftReferenceComparer : EqualityComparer<ThriftReference>
        // {
        //     public override bool Equals(ThriftReference x, ThriftReference y)
        //     {
        //         if (x == null && y == null) return true;
        //         if (x == null || y == null) return false;

        //         return x.RefType == y.RefType
        //             && x.SpanId == y.SpanId
        //             && x.TraceIdHigh == y.TraceIdHigh
        //             && x.TraceIdLow == y.TraceIdLow;
        //     }

        //     public override int GetHashCode(ThriftReference obj)
        //     {
        //         return obj.GetHashCode();
        //     }
        // }
    }

}
