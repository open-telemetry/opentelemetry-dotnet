// <copyright file="JaegerActivityConversionTest.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Implementation.Tests;

public class JaegerActivityConversionTest
{
    static JaegerActivityConversionTest()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_AllPropertiesSet(bool isRootSpan)
    {
        using var activity = CreateTestActivity(isRootSpan: isRootSpan);
        var traceIdAsInt = new Int128(activity.Context.TraceId);
        var spanIdAsInt = new Int128(activity.Context.SpanId);
        var linkTraceIdAsInt = new Int128(activity.Links.Single().Context.TraceId);
        var linkSpanIdAsInt = new Int128(activity.Links.Single().Context.SpanId);

        var jaegerSpan = activity.ToJaegerSpan();

        Assert.Equal("Name", jaegerSpan.OperationName);
        Assert.Equal(2, jaegerSpan.Logs.Count);

        Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
        Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
        Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
        Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

        Assert.Equal(activity.Links.Count(), jaegerSpan.References.Count);
        var references = jaegerSpan.References.ToArray();
        var jaegerRef = references[0];
        Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
        Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
        Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

        Assert.Equal(0x1, jaegerSpan.Flags);

        Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
        Assert.Equal(TimeSpanToMicroseconds(activity.Duration), jaegerSpan.Duration);

        var tags = jaegerSpan.Tags.ToArray();
        var tag = tags[0];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("stringKey", tag.Key);
        Assert.Equal("value", tag.VStr);
        tag = tags[1];
        Assert.Equal(JaegerTagType.LONG, tag.VType);
        Assert.Equal("longKey", tag.Key);
        Assert.Equal(1, tag.VLong);
        tag = tags[2];
        Assert.Equal(JaegerTagType.LONG, tag.VType);
        Assert.Equal("longKey2", tag.Key);
        Assert.Equal(1, tag.VLong);
        tag = tags[3];
        Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
        Assert.Equal("doubleKey", tag.Key);
        Assert.Equal(1, tag.VDouble);
        tag = tags[4];
        Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
        Assert.Equal("doubleKey2", tag.Key);
        Assert.Equal(1, tag.VDouble);
        tag = tags[5];
        Assert.Equal(JaegerTagType.BOOL, tag.VType);
        Assert.Equal("boolKey", tag.Key);
        Assert.Equal(true, tag.VBool);

        var logs = jaegerSpan.Logs.ToArray();
        var jaegerLog = logs[0];
        Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
        Assert.Equal(3, jaegerLog.Fields.Count);
        var eventFields = jaegerLog.Fields.ToArray();
        var eventField = eventFields[0];
        Assert.Equal("key", eventField.Key);
        Assert.Equal("value", eventField.VStr);
        eventField = eventFields[1];
        Assert.Equal("string_array", eventField.Key);
        Assert.Equal(@"[""a"",""b""]", eventField.VStr);
        eventField = eventFields[2];
        Assert.Equal("event", eventField.Key);
        Assert.Equal("Event1", eventField.VStr);

        Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

        jaegerLog = logs[1];
        Assert.Equal(2, jaegerLog.Fields.Count);
        eventFields = jaegerLog.Fields.ToArray();
        eventField = eventFields[0];
        Assert.Equal("key", eventField.Key);
        Assert.Equal("value", eventField.VStr);
        eventField = eventFields[1];
        Assert.Equal("event", eventField.Key);
        Assert.Equal("Event2", eventField.VStr);
    }

    [Fact]
    public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_NoAttributes()
    {
        using var activity = CreateTestActivity(setAttributes: false);
        var traceIdAsInt = new Int128(activity.Context.TraceId);
        var spanIdAsInt = new Int128(activity.Context.SpanId);
        var linkTraceIdAsInt = new Int128(activity.Links.Single().Context.TraceId);
        var linkSpanIdAsInt = new Int128(activity.Links.Single().Context.SpanId);

        var jaegerSpan = activity.ToJaegerSpan();

        Assert.Equal("Name", jaegerSpan.OperationName);
        Assert.Equal(2, jaegerSpan.Logs.Count);

        Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
        Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
        Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
        Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

        Assert.Equal(activity.Links.Count(), jaegerSpan.References.Count);
        var references = jaegerSpan.References.ToArray();
        var jaegerRef = references[0];
        Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
        Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
        Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

        Assert.Equal(0x1, jaegerSpan.Flags);

        Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
        Assert.Equal(TimeSpanToMicroseconds(activity.Duration), jaegerSpan.Duration);

        // 2 tags: span.kind & library.name.
        Assert.Equal(2, jaegerSpan.Tags.Count);

        var logs = jaegerSpan.Logs.ToArray();
        var jaegerLog = logs[0];
        Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
        Assert.Equal(3, jaegerLog.Fields.Count);
        var eventFields = jaegerLog.Fields.ToArray();
        var eventField = eventFields[0];
        Assert.Equal("key", eventField.Key);
        Assert.Equal("value", eventField.VStr);
        eventField = eventFields[2];
        Assert.Equal("event", eventField.Key);
        Assert.Equal("Event1", eventField.VStr);

        Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

        jaegerLog = logs[1];
        Assert.Equal(2, jaegerLog.Fields.Count);
        eventFields = jaegerLog.Fields.ToArray();
        eventField = eventFields[0];
        Assert.Equal("key", eventField.Key);
        Assert.Equal("value", eventField.VStr);
        eventField = eventFields[1];
        Assert.Equal("event", eventField.Key);
        Assert.Equal("Event2", eventField.VStr);
    }

    [Fact]
    public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_NoEvents()
    {
        using var activity = CreateTestActivity(addEvents: false);
        var traceIdAsInt = new Int128(activity.Context.TraceId);
        var spanIdAsInt = new Int128(activity.Context.SpanId);
        var linkTraceIdAsInt = new Int128(activity.Links.Single().Context.TraceId);
        var linkSpanIdAsInt = new Int128(activity.Links.Single().Context.SpanId);

        var jaegerSpan = activity.ToJaegerSpan();

        Assert.Equal("Name", jaegerSpan.OperationName);
        Assert.Empty(jaegerSpan.Logs);

        Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
        Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
        Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
        Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

        Assert.Equal(activity.Links.Count(), jaegerSpan.References.Count);
        var references = jaegerSpan.References.ToArray();
        var jaegerRef = references[0];
        Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
        Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
        Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

        Assert.Equal(0x1, jaegerSpan.Flags);

        Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
        Assert.Equal(TimeSpanToMicroseconds(activity.Duration), jaegerSpan.Duration);

        var tags = jaegerSpan.Tags.ToArray();
        var tag = tags[0];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("stringKey", tag.Key);
        Assert.Equal("value", tag.VStr);
        tag = tags[1];
        Assert.Equal(JaegerTagType.LONG, tag.VType);
        Assert.Equal("longKey", tag.Key);
        Assert.Equal(1, tag.VLong);
        tag = tags[2];
        Assert.Equal(JaegerTagType.LONG, tag.VType);
        Assert.Equal("longKey2", tag.Key);
        Assert.Equal(1, tag.VLong);
        tag = tags[3];
        Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
        Assert.Equal("doubleKey", tag.Key);
        Assert.Equal(1, tag.VDouble);
        tag = tags[4];
        Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
        Assert.Equal("doubleKey2", tag.Key);
        Assert.Equal(1, tag.VDouble);
        tag = tags[5];
        Assert.Equal(JaegerTagType.BOOL, tag.VType);
        Assert.Equal("boolKey", tag.Key);
        Assert.Equal(true, tag.VBool);
    }

    [Fact]
    public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_NoLinks()
    {
        using var activity = CreateTestActivity(addLinks: false, ticksToAdd: 8000);
        var traceIdAsInt = new Int128(activity.Context.TraceId);
        var spanIdAsInt = new Int128(activity.Context.SpanId);

        var jaegerSpan = activity.ToJaegerSpan();

        Assert.Equal("Name", jaegerSpan.OperationName);
        Assert.Equal(2, jaegerSpan.Logs.Count);

        Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
        Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
        Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
        Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

        Assert.Empty(jaegerSpan.References);

        Assert.Equal(0x1, jaegerSpan.Flags);

        Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
        Assert.Equal(TimeSpanToMicroseconds(activity.Duration), jaegerSpan.Duration);

        var tags = jaegerSpan.Tags.ToArray();
        var tag = tags[0];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("stringKey", tag.Key);
        Assert.Equal("value", tag.VStr);
        tag = tags[1];
        Assert.Equal(JaegerTagType.LONG, tag.VType);
        Assert.Equal("longKey", tag.Key);
        Assert.Equal(1, tag.VLong);
        tag = tags[2];
        Assert.Equal(JaegerTagType.LONG, tag.VType);
        Assert.Equal("longKey2", tag.Key);
        Assert.Equal(1, tag.VLong);
        tag = tags[3];
        Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
        Assert.Equal("doubleKey", tag.Key);
        Assert.Equal(1, tag.VDouble);
        tag = tags[4];
        Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
        Assert.Equal("doubleKey2", tag.Key);
        Assert.Equal(1, tag.VDouble);
        tag = tags[5];
        Assert.Equal(JaegerTagType.BOOL, tag.VType);
        Assert.Equal("boolKey", tag.Key);
        Assert.Equal(true, tag.VBool);

        tag = tags[6];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("int_array", tag.Key);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new[] { 1, 2 }), tag.VStr);

        tag = tags[7];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("bool_array", tag.Key);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new[] { true, false }), tag.VStr);

        tag = tags[8];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("double_array", tag.Key);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new[] { 1, 1.1 }), tag.VStr);

        tag = tags[9];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("string_array", tag.Key);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new[] { "a", "b" }), tag.VStr);

        tag = tags[10];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("obj_array", tag.Key);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new[] { 1.ToString(), false.ToString(), new object().ToString(), "string", string.Empty, null }), tag.VStr);

        // The second to last tag should be span.kind in this case
        tag = tags[tags.Length - 2];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("span.kind", tag.Key);
        Assert.Equal("client", tag.VStr);

        // The last tag should be library.name in this case
        tag = tags[tags.Length - 1];
        Assert.Equal(JaegerTagType.STRING, tag.VType);
        Assert.Equal("otel.library.name", tag.Key);
        Assert.Equal(nameof(CreateTestActivity), tag.VStr);

        var logs = jaegerSpan.Logs.ToArray();
        var jaegerLog = logs[0];
        Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
        Assert.Equal(3, jaegerLog.Fields.Count);
        var eventFields = jaegerLog.Fields.ToArray();
        var eventField = eventFields[0];
        Assert.Equal("key", eventField.Key);
        Assert.Equal("value", eventField.VStr);
        eventField = eventFields[2];
        Assert.Equal("event", eventField.Key);
        Assert.Equal("Event1", eventField.VStr);
        Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

        jaegerLog = logs[1];
        Assert.Equal(2, jaegerLog.Fields.Count);
        eventFields = jaegerLog.Fields.ToArray();
        eventField = eventFields[0];
        Assert.Equal("key", eventField.Key);
        Assert.Equal("value", eventField.VStr);
        eventField = eventFields[1];
        Assert.Equal("event", eventField.Key);
        Assert.Equal("Event2", eventField.VStr);
    }

    [Fact]
    public void JaegerActivityConverterTest_GenerateJaegerSpan_RemoteEndpointOmittedByDefault()
    {
        // Arrange
        using var span = CreateTestActivity();

        // Act
        var jaegerSpan = span.ToJaegerSpan();

        // Assert
        Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == "peer.service");
    }

    [Fact]
    public void JaegerActivityConverterTest_GenerateJaegerSpan_RemoteEndpointResolution()
    {
        // Arrange
        using var span = CreateTestActivity(
            additionalAttributes: new Dictionary<string, object>
            {
                ["net.peer.name"] = "RemoteServiceName",
            });

        // Act
        var jaegerSpan = span.ToJaegerSpan();

        // Assert
        Assert.Contains(jaegerSpan.Tags, t => t.Key == "peer.service");
        Assert.Equal("RemoteServiceName", jaegerSpan.Tags.First(t => t.Key == "peer.service").VStr);
    }

    [Fact]
    public void JaegerActivityConverterTest_GenerateJaegerSpan_PeerServiceNameIgnoredForServerSpan()
    {
        // Arrange
        using var span = CreateTestActivity(
            additionalAttributes: new Dictionary<string, object>
            {
                ["http.host"] = "DiscardedRemoteServiceName",
            },
            kind: ActivityKind.Server);

        // Act
        var jaegerSpan = span.ToJaegerSpan();

        // Assert
        Assert.Null(jaegerSpan.PeerServiceName);
        Assert.Empty(jaegerSpan.Tags.Where(t => t.Key == "peer.service"));
    }

    [Theory]
    [MemberData(nameof(RemoteEndpointPriorityTestCase.GetTestCases), MemberType = typeof(RemoteEndpointPriorityTestCase))]
    public void JaegerActivityConverterTest_GenerateJaegerSpan_RemoteEndpointResolutionPriority(RemoteEndpointPriorityTestCase testCase)
    {
        // Arrange
        using var activity = CreateTestActivity(additionalAttributes: testCase.RemoteEndpointAttributes);

        // Act
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert
        var tags = jaegerSpan.Tags.Where(t => t.Key == "peer.service");
        Assert.Single(tags);
        var tag = tags.First();
        Assert.Equal(testCase.ExpectedResult, tag.VStr);
    }

    [Fact]
    public void JaegerActivityConverterTest_NullTagValueTest()
    {
        // Arrange
        using var activity = CreateTestActivity(additionalAttributes: new Dictionary<string, object> { ["nullTag"] = null });

        // Act
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert
        Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == "nullTag");
    }

    [Theory]
    [InlineData(StatusCode.Unset, "unset", "")]
    [InlineData(StatusCode.Ok, "Ok", "")]
    [InlineData(StatusCode.Error, "ERROR", "error description")]
    [InlineData(StatusCode.Unset, "iNvAlId", "")]
    public void JaegerActivityConverterTest_Status_ErrorFlagTest(StatusCode expectedStatusCode, string statusCodeTagValue, string statusDescription)
    {
        // Arrange
        using var activity = CreateTestActivity();
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, statusCodeTagValue);
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, statusDescription);

        // Act
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert

        Assert.Equal(expectedStatusCode, activity.GetStatus().StatusCode);

        if (expectedStatusCode == StatusCode.Unset)
        {
            Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == SpanAttributeConstants.StatusCodeKey);
        }
        else
        {
            Assert.Equal(
                StatusHelper.GetTagValueForStatusCode(expectedStatusCode),
                jaegerSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).VStr);
        }

        if (expectedStatusCode == StatusCode.Error)
        {
            Assert.Contains(
                jaegerSpan.Tags, t =>
                t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName &&
                t.VType == JaegerTagType.BOOL && (t.VBool ?? false));
            Assert.Contains(
                jaegerSpan.Tags, t =>
                t.Key == SpanAttributeConstants.StatusDescriptionKey &&
                t.VType == JaegerTagType.STRING && t.VStr.Equals(statusDescription));
        }
        else
        {
            Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName);
        }
    }

    [Theory]
    [InlineData(ActivityStatusCode.Unset)]
    [InlineData(ActivityStatusCode.Ok)]
    [InlineData(ActivityStatusCode.Error)]
    public void ToJaegerSpan_Activity_Status_And_StatusDescription_is_Set(ActivityStatusCode expectedStatusCode)
    {
        // Arrange
        using var activity = CreateTestActivity();
        activity.SetStatus(expectedStatusCode);

        // Act
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert
        if (expectedStatusCode == ActivityStatusCode.Unset)
        {
            Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == SpanAttributeConstants.StatusCodeKey);
        }
        else if (expectedStatusCode == ActivityStatusCode.Ok)
        {
            Assert.Equal("OK", jaegerSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).VStr);
        }

        // expectedStatusCode is Error
        else
        {
            Assert.Equal("ERROR", jaegerSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).VStr);
        }

        if (expectedStatusCode == ActivityStatusCode.Error)
        {
            Assert.Contains(
                jaegerSpan.Tags, t =>
                t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName &&
                t.VType == JaegerTagType.BOOL && (t.VBool ?? false));
        }
        else
        {
            Assert.DoesNotContain(
                jaegerSpan.Tags, t =>
                t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName);
        }
    }

    [Fact]
    public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsOk()
    {
        // Arrange.
        using var activity = CreateTestActivity();
        const string TagDescriptionOnError = "Description when TagStatusCode is Error.";
        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, TagDescriptionOnError);

        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Act.
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert.
        Assert.Equal("OK", jaegerSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).VStr);

        Assert.Contains(jaegerSpan.Tags, t => t.Key == "otel.status_code" && t.VStr == "OK");
        Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == "otel.status_code" && t.VStr == "ERROR");
        Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName);
        Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == SpanAttributeConstants.StatusDescriptionKey &&
                                t.VType == JaegerTagType.STRING && t.VStr.Equals(TagDescriptionOnError));

        // Ensure additional Activity tags were being converted.
        Assert.Contains(jaegerSpan.Tags, t => t.Key == "myCustomTag" && t.VStr == "myCustomTagValue");
    }

    [Fact]
    public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsError()
    {
        // Arrange.
        using var activity = CreateTestActivity();
        const string StatusDescriptionOnError = "Description when ActivityStatusCode is Error.";
        activity.SetStatus(ActivityStatusCode.Error, StatusDescriptionOnError);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "OK");

        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Act.
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert.
        Assert.Contains(jaegerSpan.Tags, t => t.Key == "otel.status_code" && t.VStr == "ERROR");
        Assert.Contains(jaegerSpan.Tags, t => t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName);
        Assert.Contains(jaegerSpan.Tags, t => t.Key == SpanAttributeConstants.StatusDescriptionKey &&
                    t.VType == JaegerTagType.STRING && t.VStr.Equals(StatusDescriptionOnError));

        Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == "otel.status_code" && t.VStr == "OK");

        // Ensure additional Activity tags were being converted.
        Assert.Contains(jaegerSpan.Tags, t => t.Key == "myCustomTag" && t.VStr == "myCustomTagValue");
    }

    [Fact]
    public void ActivityDescription_Takes_precedence_Over_Status_Tags_When_ActivityStatusCodeIsError()
    {
        // Arrange.
        using var activity = CreateTestActivity();

        const string StatusDescriptionOnError = "Description when ActivityStatusCode is Error.";
        const string TagDescriptionOnError = "Description when TagStatusCode is Error.";
        activity.SetStatus(ActivityStatusCode.Error, StatusDescriptionOnError);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, TagDescriptionOnError);

        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Act.
        var jaegerSpan = activity.ToJaegerSpan();

        // Assert.
        Assert.Equal("ERROR", jaegerSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).VStr);

        Assert.Contains(
            jaegerSpan.Tags, t =>
            t.Key == JaegerActivityExtensions.JaegerErrorFlagTagName &&
            t.VType == JaegerTagType.BOOL && (t.VBool ?? false));

        Assert.Contains(
            jaegerSpan.Tags, t =>
            t.Key == SpanAttributeConstants.StatusDescriptionKey &&
            t.VType == JaegerTagType.STRING && t.VStr.Equals(StatusDescriptionOnError));

        // Ensure additional Activity tags were being converted.
        Assert.Contains(jaegerSpan.Tags, t => t.Key == "myCustomTag" && t.VStr == "myCustomTagValue");
    }

    internal static Activity CreateTestActivity(
        bool setAttributes = true,
        Dictionary<string, object> additionalAttributes = null,
        bool addEvents = true,
        bool addLinks = true,
        Resource resource = null,
        ActivityKind kind = ActivityKind.Client,
        bool isRootSpan = false,
        Status? status = null,
        long ticksToAdd = 60 * TimeSpan.TicksPerSecond)
    {
        var startTimestamp = DateTime.UtcNow;
        var endTimestamp = startTimestamp.AddTicks(ticksToAdd);
        var eventTimestamp = DateTime.UtcNow;
        var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

        var parentSpanId = isRootSpan ? default : ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });

        var attributes = new Dictionary<string, object>
        {
            { "exceptionFromToString", new MyToStringMethodThrowsAnException() },
            { "exceptionFromToStringInArray", new MyToStringMethodThrowsAnException[] { new MyToStringMethodThrowsAnException() } },
            { "stringKey", "value" },
            { "longKey", 1L },
            { "longKey2", 1 },
            { "doubleKey", 1D },
            { "doubleKey2", 1F },
            { "boolKey", true },
            { "int_array", new int[] { 1, 2 } },
            { "bool_array", new bool[] { true, false } },
            { "double_array", new double[] { 1.0, 1.1 } },
            { "string_array", new string[] { "a", "b" } },
            { "obj_array", new object[] { 1, false, new object(), "string", string.Empty, null } },
        };
        if (additionalAttributes != null)
        {
            foreach (var attribute in additionalAttributes)
            {
                attributes.Add(attribute.Key, attribute.Value);
            }
        }

        var events = new List<ActivityEvent>
        {
            new ActivityEvent(
                "Event1",
                eventTimestamp,
                new ActivityTagsCollection(new Dictionary<string, object>
                {
                    { "key", "value" },
                    { "string_array", new string[] { "a", "b" } },
                })),
            new ActivityEvent(
                "Event2",
                eventTimestamp,
                new ActivityTagsCollection(new Dictionary<string, object>
                {
                    { "key", "value" },
                })),
        };

        var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

        using var activitySource = new ActivitySource(nameof(CreateTestActivity));

        var tags = setAttributes ?
                attributes
                : null;
        var links = addLinks ?
                new[]
                {
                    new ActivityLink(new ActivityContext(
                        traceId,
                        linkedSpanId,
                        ActivityTraceFlags.Recorded)),
                }
                : null;

        using var activity = activitySource.StartActivity(
            "Name",
            kind,
            parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
            tags,
            links,
            startTime: startTimestamp);

        if (addEvents)
        {
            foreach (var evnt in events)
            {
                activity.AddEvent(evnt);
            }
        }

        if (status.HasValue)
        {
            activity.SetStatus(status.Value);
        }

        activity.SetEndTime(endTimestamp);
        activity.Stop();

        return activity;
    }

    private static long TimeSpanToMicroseconds(TimeSpan timeSpan)
    {
        return timeSpan.Ticks / (TimeSpan.TicksPerMillisecond / 1000);
    }

    public class RemoteEndpointPriorityTestCase
    {
        public string Name { get; set; }

        public string ExpectedResult { get; set; }

        public Dictionary<string, object> RemoteEndpointAttributes { get; set; }

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Highest priority name = net.peer.name",
                    ExpectedResult = "RemoteServiceName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "RemoteServiceName",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Highest priority name = SemanticConventions.AttributePeerService",
                    ExpectedResult = "RemoteServiceName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        [SemanticConventions.AttributePeerService] = "RemoteServiceName",
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "DiscardedRemoteServiceName",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Only has net.peer.name and net.peer.port",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.port"] = "1234",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "net.peer.port is an int",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.port"] = 1234,
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Has net.peer.name and net.peer.port",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Has net.peer.ip and net.peer.port",
                    ExpectedResult = "1.2.3.4:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.ip"] = "1.2.3.4",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Has net.peer.name, net.peer.ip, and net.peer.port",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.ip"] = "1.2.3.4",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
            };
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    private class MyToStringMethodThrowsAnException
    {
        public override string ToString()
        {
            throw new Exception("Nope.");
        }
    }
}
