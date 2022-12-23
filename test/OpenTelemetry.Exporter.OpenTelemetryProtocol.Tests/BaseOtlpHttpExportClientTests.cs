// <copyright file="BaseOtlpHttpExportClientTests.cs" company="OpenTelemetry Authors">
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

using System.Net.Http;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class BaseOtlpHttpExportClientTests
    {
        [Theory]
        [InlineData(null, null, "http://localhost:4318/signal/path")]
        [InlineData(null, "http://from.otel.exporter.env.var", "http://from.otel.exporter.env.var/signal/path")]
        [InlineData("https://custom.host", null, "https://custom.host")]
        [InlineData("http://custom.host:44318/custom/path", null, "http://custom.host:44318/custom/path")]
        [InlineData("https://custom.host", "http://from.otel.exporter.env.var", "https://custom.host")]
        public void ValidateOtlpHttpExportClientEndpoint(string optionEndpoint, string endpointEnvVar, string expectedExporterEndpoint)
        {
            try
            {
                Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, endpointEnvVar);

                OtlpExporterOptions options = new() { Protocol = OtlpExportProtocol.HttpProtobuf };

                if (optionEndpoint != null)
                {
                    options.Endpoint = new Uri(optionEndpoint);
                }

                var exporterClient = new TestOtlpHttpExportClient(options, new HttpClient());
                Assert.Equal(new Uri(expectedExporterEndpoint), exporterClient.Endpoint);
            }
            finally
            {
                Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, null);
            }
        }

        internal class TestOtlpHttpExportClient : BaseOtlpHttpExportClient<string>
        {
            public TestOtlpHttpExportClient(OtlpExporterOptions options, HttpClient httpClient)
                : base(options, httpClient, "signal/path")
            {
            }

            protected override HttpContent CreateHttpContent(string exportRequest)
            {
                throw new NotImplementedException();
            }
        }
    }
}
