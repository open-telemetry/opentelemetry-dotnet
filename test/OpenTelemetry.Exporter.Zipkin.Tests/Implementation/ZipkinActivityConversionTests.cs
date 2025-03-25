// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Zipkin.Tests;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinActivityConversionTests
{
    private const string ZipkinSpanName = "Name";
    private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new("TestService");

    [Fact]
    public void ToZipkinSpan_AllPropertiesSet()
    {
        // Arrange
        using var activity = ZipkinActivitySource.CreateTestActivity();

        // Act & Assert
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        Assert.Equal(ZipkinSpanName, zipkinSpan.Name);

        Assert.Equal(activity.TraceId.ToHexString(), zipkinSpan.TraceId);
        Assert.Equal(activity.SpanId.ToHexString(), zipkinSpan.Id);

        Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), zipkinSpan.Timestamp);
        Assert.Equal((long)(activity.Duration.TotalMilliseconds * 1000), zipkinSpan.Duration);

        int counter = 0;
        var tagsArray = zipkinSpan.Tags.ToArray();

        foreach (var tags in activity.TagObjects)
        {
            Assert.Equal(tagsArray[counter].Key, tags.Key);
            Assert.Equal(tagsArray[counter++].Value, tags.Value);
        }

        foreach (var annotation in zipkinSpan.Annotations)
        {
            // Timestamp is same in both events
            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), annotation.Timestamp);
        }
    }

    [Fact]
    public void ToZipkinSpan_NoEvents()
    {
        // Arrange
        using var activity = ZipkinActivitySource.CreateTestActivity(addEvents: false);

        // Act & Assert
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        Assert.Equal(ZipkinSpanName, zipkinSpan.Name);
        Assert.Empty(zipkinSpan.Annotations);
        Assert.Equal(activity.TraceId.ToHexString(), zipkinSpan.TraceId);
        Assert.Equal(activity.SpanId.ToHexString(), zipkinSpan.Id);

        int counter = 0;
        var tagsArray = zipkinSpan.Tags.ToArray();

        foreach (var tags in activity.TagObjects)
        {
            Assert.Equal(tagsArray[counter].Key, tags.Key);
            Assert.Equal(tagsArray[counter++].Value, tags.Value);
        }

        Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), zipkinSpan.Timestamp);
        Assert.Equal((long)activity.Duration.TotalMilliseconds * 1000, zipkinSpan.Duration);
    }

    [Theory]
    [InlineData(StatusCode.Unset, "unset")]
    [InlineData(StatusCode.Ok, "Ok")]
    [InlineData(StatusCode.Error, "ERROR")]
    [InlineData(StatusCode.Unset, "iNvAlId")]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ToZipkinSpan_Status_ErrorFlagTest(StatusCode expectedStatusCode, string statusCodeTagValue)
    {
        // Arrange
        using var activity = ZipkinActivitySource.CreateTestActivity();
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, statusCodeTagValue);

        // Act
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        // Assert

        Assert.Equal(expectedStatusCode, activity.GetStatus().StatusCode);

        if (expectedStatusCode == StatusCode.Unset)
        {
            Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == SpanAttributeConstants.StatusCodeKey);
        }
        else
        {
            Assert.Equal(
                StatusHelper.GetTagValueForStatusCode(expectedStatusCode),
                zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
        }

        if (expectedStatusCode == StatusCode.Error)
        {
            Assert.Contains(zipkinSpan.Tags, t => t.Key == "error" && ((string?)t.Value)?.Length == 0);
        }
        else
        {
            Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == "error");
        }
    }

    [Theory]
    [InlineData(ActivityStatusCode.Unset)]
    [InlineData(ActivityStatusCode.Ok)]
    [InlineData(ActivityStatusCode.Error)]
    public void ToZipkinSpan_Activity_Status_And_StatusDescription_is_Set(ActivityStatusCode expectedStatusCode)
    {
        // Arrange.
        const string description = "Description when ActivityStatusCode is Error.";
        using var activity = ZipkinActivitySource.CreateTestActivity();
        activity.SetStatus(expectedStatusCode, description);

        // Act.
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        // Assert.
        if (expectedStatusCode == ActivityStatusCode.Unset)
        {
            Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == SpanAttributeConstants.StatusCodeKey);
        }
        else if (expectedStatusCode == ActivityStatusCode.Ok)
        {
            Assert.Equal("OK", zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
        }

        // expectedStatusCode is Error
        else
        {
            Assert.Equal("ERROR", zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
        }

        if (expectedStatusCode == ActivityStatusCode.Error)
        {
            Assert.Contains(
                zipkinSpan.Tags, t =>
                t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
                (string?)t.Value == description);
        }
        else
        {
            Assert.DoesNotContain(
                zipkinSpan.Tags, t =>
                t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName);
        }
    }

    [Fact]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsOk()
    {
        // Arrange.
        using var activity = ZipkinActivitySource.CreateTestActivity();
        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");

        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Act.
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        // Assert.
        Assert.Equal("OK", zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);

        Assert.Contains(zipkinSpan.Tags, t => t.Key == "otel.status_code" && (string?)t.Value == "OK");
        Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == "otel.status_code" && (string?)t.Value == "ERROR");

        // Ensure additional Activity tags were being converted.
        Assert.Contains(zipkinSpan.Tags, t => t.Key == "myCustomTag" && (string?)t.Value == "myCustomTagValue");
        Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName);
    }

    [Fact]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsError()
    {
        // Arrange.
        using var activity = ZipkinActivitySource.CreateTestActivity();

        const string StatusDescriptionOnError = "Description when ActivityStatusCode is Error.";
        const string TagDescriptionOnError = "Description when TagStatusCode is Error.";
        activity.SetStatus(ActivityStatusCode.Error, StatusDescriptionOnError);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, TagDescriptionOnError);

        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Act.
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        // Assert.
        Assert.Equal("ERROR", zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);

        // ActivityStatusDescription takes higher precedence.
        Assert.Contains(
            zipkinSpan.Tags, t =>
            t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
            (string?)t.Value == StatusDescriptionOnError);
        Assert.DoesNotContain(
            zipkinSpan.Tags, t =>
            t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
            (string?)t.Value == TagDescriptionOnError);

        // Ensure additional Activity tags were being converted.
        Assert.Contains(zipkinSpan.Tags, t => t.Key == "myCustomTag" && (string?)t.Value == "myCustomTagValue");
    }

    [Fact]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsError_SettingTagFirst()
    {
        // Arrange.
        using var activity = ZipkinActivitySource.CreateTestActivity();

        const string StatusDescriptionOnError = "Description when ActivityStatusCode is Error.";
        const string TagDescriptionOnError = "Description when TagStatusCode is Error.";
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, TagDescriptionOnError);
        activity.SetStatus(ActivityStatusCode.Error, StatusDescriptionOnError);

        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Act.
        var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

        // Assert.
        Assert.Equal("ERROR", zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);

        // ActivityStatusDescription takes higher precedence.
        Assert.Contains(
            zipkinSpan.Tags, t =>
            t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
            (string?)t.Value == StatusDescriptionOnError);
        Assert.DoesNotContain(
            zipkinSpan.Tags, t =>
            t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
            (string?)t.Value == TagDescriptionOnError);

        // Ensure additional Activity tags were being converted.
        Assert.Contains(zipkinSpan.Tags, t => t.Key == "myCustomTag" && (string?)t.Value == "myCustomTagValue");
    }
}
