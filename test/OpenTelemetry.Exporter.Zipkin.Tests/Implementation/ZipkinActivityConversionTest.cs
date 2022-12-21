// <copyright file="ZipkinActivityConversionTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Zipkin.Tests;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests
{
    public class ZipkinActivityConversionTest
    {
        private const string ZipkinSpanName = "Name";
        private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new("TestService");

        [Fact]
        public void ToZipkinSpan_AllPropertiesSet()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity();

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
            var activity = ZipkinExporterTests.CreateTestActivity(addEvents: false);

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
        public void ToZipkinSpan_Status_ErrorFlagTest(StatusCode expectedStatusCode, string statusCodeTagValue)
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity();
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
                Assert.Contains(zipkinSpan.Tags, t => t.Key == "error" && (string)t.Value == string.Empty);
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
            var activity = ZipkinExporterTests.CreateTestActivity();
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
                    (string)t.Value == description);
            }
            else
            {
                Assert.DoesNotContain(
                    zipkinSpan.Tags, t =>
                    t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName);
            }
        }

        [Fact]
        public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsOk()
        {
            // Arrange.
            var activity = ZipkinExporterTests.CreateTestActivity();
            activity.SetStatus(ActivityStatusCode.Ok);
            activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");

            // Enrich activity with additional tags.
            activity.SetTag("myCustomTag", "myCustomTagValue");

            // Act.
            var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

            // Assert.
            Assert.Equal("OK", zipkinSpan.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);

            Assert.Contains(zipkinSpan.Tags, t => t.Key == "otel.status_code" && (string)t.Value == "OK");
            Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == "otel.status_code" && (string)t.Value == "ERROR");

            // Ensure additional Activity tags were being converted.
            Assert.Contains(zipkinSpan.Tags, t => t.Key == "myCustomTag" && (string)t.Value == "myCustomTagValue");
            Assert.DoesNotContain(zipkinSpan.Tags, t => t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName);
        }

        [Fact]
        public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsError()
        {
            // Arrange.
            var activity = ZipkinExporterTests.CreateTestActivity();

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
                (string)t.Value == StatusDescriptionOnError);
            Assert.DoesNotContain(
                zipkinSpan.Tags, t =>
                t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
                (string)t.Value == TagDescriptionOnError);

            // Ensure additional Activity tags were being converted.
            Assert.Contains(zipkinSpan.Tags, t => t.Key == "myCustomTag" && (string)t.Value == "myCustomTagValue");
        }

        [Fact]
        public void ActivityStatus_Takes_precedence_Over_Status_Tags_ActivityStatusCodeIsError_SettingTagFirst()
        {
            // Arrange.
            var activity = ZipkinExporterTests.CreateTestActivity();

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
                (string)t.Value == StatusDescriptionOnError);
            Assert.DoesNotContain(
                zipkinSpan.Tags, t =>
                t.Key == ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName &&
                (string)t.Value == TagDescriptionOnError);

            // Ensure additional Activity tags were being converted.
            Assert.Contains(zipkinSpan.Tags, t => t.Key == "myCustomTag" && (string)t.Value == "myCustomTagValue");
        }
    }
}
