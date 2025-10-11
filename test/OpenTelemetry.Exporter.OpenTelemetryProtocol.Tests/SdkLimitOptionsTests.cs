// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class SdkLimitOptionsTests : IDisposable
{
    public SdkLimitOptionsTests()
    {
        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
    }

    [Fact]
    public void SdkLimitOptionsDefaults()
    {
        var options = new SdkLimitOptions();

        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(128, options.AttributeCountLimit);
        Assert.Null(options.SpanAttributeValueLengthLimit);
        Assert.Equal(128, options.SpanAttributeCountLimit);
        Assert.Equal(128, options.SpanEventCountLimit);
        Assert.Equal(128, options.SpanLinkCountLimit);
        Assert.Equal(128, options.SpanEventAttributeCountLimit);
        Assert.Equal(128, options.SpanLinkAttributeCountLimit);
        Assert.Null(options.LogRecordAttributeValueLengthLimit);
        Assert.Equal(128, options.LogRecordAttributeCountLimit);
    }

    [Fact]
    public void SdkLimitOptionsIsInitializedFromEnvironment()
    {
        Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", "10");
        Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_COUNT_LIMIT", "10");
        Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", "20");
        Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", "20");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", "10");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", "10");
        Environment.SetEnvironmentVariable("OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", "30");
        Environment.SetEnvironmentVariable("OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", "30");

        var options = new SdkLimitOptions();

        Assert.Equal(10, options.AttributeValueLengthLimit);
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(20, options.SpanAttributeValueLengthLimit);
        Assert.Equal(20, options.SpanAttributeCountLimit);
        Assert.Equal(10, options.SpanEventCountLimit);
        Assert.Equal(10, options.SpanLinkCountLimit);
        Assert.Equal(30, options.SpanEventAttributeCountLimit);
        Assert.Equal(30, options.SpanLinkAttributeCountLimit);
    }

    [Fact]
    public void SpanAttributeValueLengthLimitFallback()
    {
        var options = new SdkLimitOptions();

        options.AttributeValueLengthLimit = 10;
        Assert.Equal(10, options.AttributeValueLengthLimit);
        Assert.Equal(10, options.SpanAttributeValueLengthLimit);
        Assert.Equal(10, options.LogRecordAttributeValueLengthLimit);

        options.SpanAttributeValueLengthLimit = 20;
        options.LogRecordAttributeValueLengthLimit = 21;
        Assert.Equal(10, options.AttributeValueLengthLimit);
        Assert.Equal(20, options.SpanAttributeValueLengthLimit);
        Assert.Equal(21, options.LogRecordAttributeValueLengthLimit);

        options.SpanAttributeValueLengthLimit = null;
        options.LogRecordAttributeValueLengthLimit = null;
        Assert.Equal(10, options.AttributeValueLengthLimit);
        Assert.Null(options.SpanAttributeValueLengthLimit);
        Assert.Null(options.LogRecordAttributeValueLengthLimit);
    }

    [Fact]
    public void SpanAttributeCountLimitFallback()
    {
        var options = new SdkLimitOptions();

        options.AttributeCountLimit = 10;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(10, options.SpanAttributeCountLimit);
        Assert.Equal(10, options.SpanEventAttributeCountLimit);
        Assert.Equal(10, options.SpanLinkAttributeCountLimit);
        Assert.Equal(10, options.LogRecordAttributeCountLimit);

        options.SpanAttributeCountLimit = 20;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(20, options.SpanAttributeCountLimit);
        Assert.Equal(20, options.SpanEventAttributeCountLimit);
        Assert.Equal(20, options.SpanLinkAttributeCountLimit);

        options.SpanEventAttributeCountLimit = 30;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(20, options.SpanAttributeCountLimit);
        Assert.Equal(30, options.SpanEventAttributeCountLimit);
        Assert.Equal(20, options.SpanLinkAttributeCountLimit);

        options.SpanLinkAttributeCountLimit = 40;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(20, options.SpanAttributeCountLimit);
        Assert.Equal(30, options.SpanEventAttributeCountLimit);
        Assert.Equal(40, options.SpanLinkAttributeCountLimit);

        options.SpanLinkAttributeCountLimit = null;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(20, options.SpanAttributeCountLimit);
        Assert.Equal(30, options.SpanEventAttributeCountLimit);
        Assert.Null(options.SpanLinkAttributeCountLimit);

        options.SpanEventAttributeCountLimit = null;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(20, options.SpanAttributeCountLimit);
        Assert.Null(options.SpanEventAttributeCountLimit);
        Assert.Null(options.SpanLinkAttributeCountLimit);

        options.SpanAttributeCountLimit = null;
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Null(options.SpanAttributeCountLimit);
        Assert.Null(options.SpanEventAttributeCountLimit);
        Assert.Null(options.SpanLinkAttributeCountLimit);
    }

    [Fact]
    public void SdkLimitOptionsUsingIConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT"] = "23",
            ["OTEL_ATTRIBUTE_COUNT_LIMIT"] = "24",
            ["OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT"] = "25",
            ["OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT"] = "26",
            ["OTEL_SPAN_EVENT_COUNT_LIMIT"] = "27",
            ["OTEL_SPAN_LINK_COUNT_LIMIT"] = "28",
            ["OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT"] = "29",
            ["OTEL_LINK_ATTRIBUTE_COUNT_LIMIT"] = "30",
            ["OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT"] = "31",
            ["OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT"] = "32",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new SdkLimitOptions(configuration);

        Assert.Equal(23, options.AttributeValueLengthLimit);
        Assert.Equal(24, options.AttributeCountLimit);
        Assert.Equal(25, options.SpanAttributeValueLengthLimit);
        Assert.Equal(26, options.SpanAttributeCountLimit);
        Assert.Equal(27, options.SpanEventCountLimit);
        Assert.Equal(28, options.SpanLinkCountLimit);
        Assert.Equal(29, options.SpanEventAttributeCountLimit);
        Assert.Equal(30, options.SpanLinkAttributeCountLimit);
        Assert.Equal(31, options.LogRecordAttributeValueLengthLimit);
        Assert.Equal(32, options.LogRecordAttributeCountLimit);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_COUNT_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT", null);
        Environment.SetEnvironmentVariable("OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT", null);
    }
}
