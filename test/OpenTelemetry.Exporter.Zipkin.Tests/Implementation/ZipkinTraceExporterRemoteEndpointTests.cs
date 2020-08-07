// <copyright file="ZipkinTraceExporterRemoteEndpointTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests.Implementation
{
    public class ZipkinTraceExporterRemoteEndpointTests
    {
        private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new ZipkinEndpoint("TestService");

        [Fact]
        public void ZipkinSpanConverterTest_GenerateSpan_RemoteEndpointOmittedByDefault()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity();

            // Act & Assert
            var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

            Assert.Null(zipkinSpan.RemoteEndpoint);
        }

        [Fact]
        public void ZipkinSpanConverterTest_GenerateSpan_RemoteEndpointResolution()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["net.peer.name"] = "RemoteServiceName",
                });

            // Act & Assert
            var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
            Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
        }

        [Fact]
        public void ZipkinSpanConverterTest_GenerateSpan_RemoteEndpointResolutionPriority()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["http.host"] = "DiscardedRemoteServiceName",
                    ["net.peer.name"] = "RemoteServiceName",
                    ["peer.hostname"] = "DiscardedRemoteServiceName",
                });

            // Act & Assert
            var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
            Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
        }
    }
}
