// <copyright file="OtlpExporterOptionsExtensionsTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;
using Xunit.Sdk;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpExporterOptionsExtensionsTests
    {
        [Theory]
        [InlineData("key=value", new string[] { "key" }, new string[] { "value" })]
        [InlineData("key1=value1,key2=value2", new string[] { "key1", "key2" }, new string[] { "value1", "value2" })]
        [InlineData("key1 = value1, key2=value2 ", new string[] { "key1", "key2" }, new string[] { "value1", "value2" })]
        [InlineData("key==value", new string[] { "key" }, new string[] { "=value" })]
        [InlineData("access-token=abc=/123,timeout=1234", new string[] { "access-token", "timeout" }, new string[] { "abc=/123", "1234" })]
        [InlineData("key1=value1;key2=value2", new string[] { "key1" }, new string[] { "value1;key2=value2" })] // semicolon is not treated as a delimeter (https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables)
        public void GetMetadataFromHeadersWorksCorrectFormat(string headers, string[] keys, string[] values)
        {
            var options = new OtlpExporterOptions();
            options.Headers = headers;
            var metadata = options.GetMetadataFromHeaders();

            Assert.Equal(keys.Length, metadata.Count);

            for (int i = 0; i < keys.Length; i++)
            {
                Assert.Contains(metadata, entry => entry.Key == keys[i] && entry.Value == values[i]);
            }
        }

        [Theory]
        [InlineData("headers")]
        [InlineData("key,value")]
        public void GetMetadataFromHeadersThrowsExceptionOnInvalidFormat(string headers)
        {
            try
            {
                var options = new OtlpExporterOptions();
                options.Headers = headers;
                var metadata = options.GetMetadataFromHeaders();
            }
            catch (Exception ex)
            {
                Assert.IsType<ArgumentException>(ex);
                Assert.Equal("Headers provided in an invalid format.", ex.Message);
                return;
            }

            throw new XunitException("GetMetadataFromHeaders did not throw an exception for invalid input headers");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void GetHeaders_NoOptionHeaders_ReturnsEmptyHeadres(string optionHeaders)
        {
            var options = new OtlpExporterOptions
            {
                Headers = optionHeaders,
            };

            var headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));

            Assert.Empty(headers);
        }

        [Theory]
        [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcTraceExportClient))]
        [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient))]
        public void GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient(OtlpExportProtocol protocol, Type expectedExportClientType)
        {
            var options = new OtlpExporterOptions
            {
                Protocol = protocol,
            };

            var exportClient = options.GetTraceExportClient();

            Assert.Equal(expectedExportClientType, exportClient.GetType());
        }

        [Fact]
        public void GetTraceExportClient_UnsupportedProtocol_Throws()
        {
            var options = new OtlpExporterOptions
            {
                Protocol = (OtlpExportProtocol)123,
            };

            Assert.Throws<NotSupportedException>(() => options.GetTraceExportClient());
        }

        [Theory]
        [InlineData("grpc", OtlpExportProtocol.Grpc)]
        [InlineData("http/protobuf", OtlpExportProtocol.HttpProtobuf)]
        [InlineData("unsupported", null)]
        public void ToOtlpExportProtocol_Protocol_MapsToCorrectValue(string protocol, OtlpExportProtocol? expectedExportProtocol)
        {
            var exportProtocol = protocol.ToOtlpExportProtocol();

            Assert.Equal(expectedExportProtocol, exportProtocol);
        }

        [Theory]
        [InlineData("http://test:8888", "http://test:8888/v1/traces")]
        [InlineData("http://test:8888/", "http://test:8888/v1/traces")]
        [InlineData("http://test:8888/v1/traces", "http://test:8888/v1/traces")]
        [InlineData("http://test:8888/v1/traces/", "http://test:8888/v1/traces/")]
        public void AppendPathIfNotPresent_TracesPath_AppendsCorrectly(string inputUri, string expectedUri)
        {
            var uri = new Uri(inputUri, UriKind.Absolute);

            var resultUri = uri.AppendPathIfNotPresent(OtlpExporterOptions.TracesExportPath);

            Assert.Equal(expectedUri, resultUri.AbsoluteUri);
        }
    }
}
