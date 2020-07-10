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
using System.Linq;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests.Implementation
{
    public class ZipkinActivityConversionTest
    {
        private const string ZipkinSpanName = "Name";
        private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new ZipkinEndpoint("TestService");

        [Fact]
        public void ZipkinActivityConversion_ToZipkinSpan_AllPropertiesSet()
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
            var tagsArray = zipkinSpan.Tags.Value.ToArray();

            foreach (var tags in activity.Tags)
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
        public void ZipkinActivityConversion_ToZipkinSpan_NoEvents()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity(addEvents: false);

            // Act & Assert
            var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);

            Assert.Equal(ZipkinSpanName, zipkinSpan.Name);
            Assert.Empty(zipkinSpan.Annotations.Value);
            Assert.Equal(activity.TraceId.ToHexString(), zipkinSpan.TraceId);
            Assert.Equal(activity.SpanId.ToHexString(), zipkinSpan.Id);

            int counter = 0;
            var tagsArray = zipkinSpan.Tags.Value.ToArray();

            foreach (var tags in activity.Tags)
            {
                Assert.Equal(tagsArray[counter].Key, tags.Key);
                Assert.Equal(tagsArray[counter++].Value, tags.Value);
            }

            Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), zipkinSpan.Timestamp);
            Assert.Equal((long)activity.Duration.TotalMilliseconds * 1000, zipkinSpan.Duration);
        }
    }
}
