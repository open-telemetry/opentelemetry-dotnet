// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Resources;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public sealed class OtlpArrayTagWriterTests : IDisposable
{
    private readonly ProtobufOtlpTagWriter.OtlpArrayTagWriter arrayTagWriter;
    private readonly ActivityListener activityListener;

    static OtlpArrayTagWriterTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    public OtlpArrayTagWriterTests()
    {
        this.arrayTagWriter = new ProtobufOtlpTagWriter.OtlpArrayTagWriter();
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.activityListener);
    }

    [Fact]
    public void BeginWriteArray_InitializesArrayState()
    {
        // Act
        var arrayState = this.arrayTagWriter.BeginWriteArray();

        // Assert
        Assert.NotNull(arrayState.Buffer);
        Assert.Equal(0, arrayState.WritePosition);
        Assert.Equal(2048, arrayState.Buffer.Length);
    }

    [Fact]
    public void WriteNullValue_AddsNullValueToBuffer()
    {
        // Arrange
        var arrayState = this.arrayTagWriter.BeginWriteArray();

        // Act
        this.arrayTagWriter.WriteNullValue(ref arrayState);

        // Assert
        // Check that the buffer contains the correct tag and length for a null value
        Assert.True(arrayState.WritePosition > 0);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void WriteIntegralValue_WritesIntegralValueToBuffer(long value)
    {
        // Arrange
        var arrayState = this.arrayTagWriter.BeginWriteArray();

        // Act
        this.arrayTagWriter.WriteIntegralValue(ref arrayState, value);

        // Assert
        Assert.True(arrayState.WritePosition > 0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void WriteFloatingPointValue_WritesFloatingPointValueToBuffer(double value)
    {
        // Arrange
        var arrayState = this.arrayTagWriter.BeginWriteArray();

        // Act
        this.arrayTagWriter.WriteFloatingPointValue(ref arrayState, value);

        // Assert
        Assert.True(arrayState.WritePosition > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteBooleanValue_WritesBooleanValueToBuffer(bool value)
    {
        // Arrange
        var arrayState = this.arrayTagWriter.BeginWriteArray();

        // Act
        this.arrayTagWriter.WriteBooleanValue(ref arrayState, value);

        // Assert
        Assert.True(arrayState.WritePosition > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("test")]
    public void WriteStringValue_WritesStringValueToBuffer(string value)
    {
        // Arrange
        var arrayState = this.arrayTagWriter.BeginWriteArray();

        // Act
        this.arrayTagWriter.WriteStringValue(ref arrayState, value.AsSpan());

        // Assert
        Assert.True(arrayState.WritePosition > 0);
    }

    [Fact]
    public void TryResize_SucceedsInitially()
    {
        // Act
        this.arrayTagWriter.BeginWriteArray();
        bool result = this.arrayTagWriter.TryResize();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryResize_RepeatedResizingStopsAtMaxBufferSize()
    {
        // Arrange
        var arrayState = this.arrayTagWriter.BeginWriteArray();
        bool resizeResult = true;

        // Act: Repeatedly attempt to resize until reaching maximum buffer size
        while (resizeResult)
        {
            resizeResult = this.arrayTagWriter.TryResize();
        }

        // Assert
        Assert.False(resizeResult, "Buffer should not resize beyond the maximum allowed size.");
    }

    [Fact]
    public void SerializeLargeArrayExceeding2MB_TruncatesInOtlpSpan()
    {
        // Create a large array exceeding 2 MB
        var largeArray = new string[512 * 1024];
        for (int i = 0; i < largeArray.Length; i++)
        {
            largeArray[i] = "1234";
        }

        var lessthat1MBArray = new string[256 * 4];
        for (int i = 0; i < lessthat1MBArray.Length; i++)
        {
            lessthat1MBArray[i] = "1234";
        }

        var tags = new ActivityTagsCollection
        {
            new("lessthat1MBArray", lessthat1MBArray),
            new("StringArray", new string?[] { "12345" }),
            new("LargeArray", largeArray),
        };

        using var activitySource = new ActivitySource(nameof(this.SerializeLargeArrayExceeding2MB_TruncatesInOtlpSpan));
        using var activity = activitySource.StartActivity("activity", ActivityKind.Server, default(ActivityContext), tags);

        Assert.NotNull(activity);

        var otlpSpan = ToOtlpSpanWithExtendedBuffer(new SdkLimitOptions(), activity);

        Assert.NotNull(otlpSpan);
        Assert.Equal(3, otlpSpan.Attributes.Count);
        var keyValue = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == "StringArray");
        Assert.NotNull(keyValue);
        Assert.Equal("12345", keyValue.Value.ArrayValue.Values[0].StringValue);

        // The string is too large, hence not evaluating the content.
        keyValue = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == "lessthat1MBArray");
        Assert.NotNull(keyValue);

        keyValue = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == "LargeArray");
        Assert.NotNull(keyValue);
        Assert.Equal("TRUNCATED", keyValue.Value.StringValue);
    }

    [Fact]
    public void LargeArray_WithSmallBaseBuffer_ThrowsExceptionOnWriteSpan()
    {
        var lessthat1MBArray = new string[256 * 256];
        for (int i = 0; i < lessthat1MBArray.Length; i++)
        {
            lessthat1MBArray[i] = "1234";
        }

        var tags = new ActivityTagsCollection
        {
            new("lessthat1MBArray", lessthat1MBArray),
        };

        using var activitySource = new ActivitySource(nameof(this.LargeArray_WithSmallBaseBuffer_ThrowsExceptionOnWriteSpan));
        using var activity = activitySource.StartActivity("root", ActivityKind.Server, default(ActivityContext), tags);

        Assert.NotNull(activity);
        Assert.Throws<ArgumentException>(() => ToOtlpSpan(new SdkLimitOptions(), activity));
    }

    [Fact]
    public void LargeArray_WithSmallBaseBuffer_ExpandsOnTraceData()
    {
        var lessthat1MBArray = new string[256 * 256];
        for (int i = 0; i < lessthat1MBArray.Length; i++)
        {
            lessthat1MBArray[i] = "1234";
        }

        var tags = new ActivityTagsCollection
        {
            new("lessthat1MBArray", lessthat1MBArray),
        };

        using var activitySource = new ActivitySource(nameof(this.LargeArray_WithSmallBaseBuffer_ExpandsOnTraceData));
        using var activity = activitySource.StartActivity("root", ActivityKind.Server, default(ActivityContext), tags);

        Assert.NotNull(activity);
        var batch = new Batch<Activity>([activity], 1);
        RunTest(new(), batch);

        void RunTest(SdkLimitOptions sdkOptions, Batch<Activity> batch)
        {
            var buffer = new byte[4096];
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, sdkOptions, ResourceBuilder.CreateEmpty().Build(), batch);
            using var stream = new MemoryStream(buffer, 0, writePosition);
            var tracesData = OtlpTrace.TracesData.Parser.ParseFrom(stream);
            var request = new OtlpCollector.ExportTraceServiceRequest();
            request.ResourceSpans.Add(tracesData.ResourceSpans);

            // Buffer should be expanded to accommodate the large array.
            Assert.True(buffer.Length > 4096);

            Assert.Single(request.ResourceSpans);
            var scopeSpans = request.ResourceSpans.First().ScopeSpans;
            Assert.Single(scopeSpans);
            var otlpSpan = scopeSpans.First().Spans.First();
            Assert.NotNull(otlpSpan);

            // The string is too large, hence not evaluating the content.
            var keyValue = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == "lessthat1MBArray");
            Assert.NotNull(keyValue);
        }
    }

    public void Dispose()
    {
        // Clean up the thread buffer after each test
        ProtobufOtlpTagWriter.OtlpArrayTagWriter.ThreadBuffer = null;
        this.activityListener.Dispose();
    }

    private static OtlpTrace.Span? ToOtlpSpan(SdkLimitOptions sdkOptions, Activity activity)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpTraceSerializer.WriteSpan(buffer, 0, sdkOptions, activity);
        using var stream = new MemoryStream(buffer, 0, writePosition);
        var scopeSpans = OtlpTrace.ScopeSpans.Parser.ParseFrom(stream);
        return scopeSpans.Spans.FirstOrDefault();
    }

    private static OtlpTrace.Span? ToOtlpSpanWithExtendedBuffer(SdkLimitOptions sdkOptions, Activity activity)
    {
        var buffer = new byte[4194304];
        var writePosition = ProtobufOtlpTraceSerializer.WriteSpan(buffer, 0, sdkOptions, activity);
        using var stream = new MemoryStream(buffer, 0, writePosition);
        var scopeSpans = OtlpTrace.ScopeSpans.Parser.ParseFrom(stream);
        return scopeSpans.Spans.FirstOrDefault();
    }
}
