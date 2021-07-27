// <copyright file="OtlpExporterOptionsTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpExporterOptionsTests : IDisposable
    {
        public OtlpExporterOptionsTests()
        {
            ClearEnvVars();
        }

        public void Dispose()
        {
            ClearEnvVars();
        }

        [Fact]
        public void OtlpExporterOptions_Defaults()
        {
            var options = new OtlpExporterOptions();

            Assert.Equal(new Uri("http://localhost:4317"), options.Endpoint);
            Assert.Null(options.Headers);
            Assert.Equal(10000, options.TimeoutMilliseconds);
        }

        [Fact]
        public void OtlpExporterOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "http://test:8888");
            Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, "A=2,B=3");
            Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, "2000");

            var options = new OtlpExporterOptions();

            Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
            Assert.Equal("A=2,B=3", options.Headers);
            Assert.Equal(2000, options.TimeoutMilliseconds);
        }

        [Fact]
        public void OtlpExporterOptions_InvalidEndpointVariableOverride()
        {
            Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "invalid");

            var options = new OtlpExporterOptions();

            Assert.Equal(new Uri("http://localhost:4317"), options.Endpoint); // use default
        }

        [Fact]
        public void OtlpExporterOptions_InvalidTimeoutVariableOverride()
        {
            Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, "invalid");

            var options = new OtlpExporterOptions();

            Assert.Equal(10000, options.TimeoutMilliseconds); // use default
        }

        [Fact]
        public void OtlpExporterOptions_SetterOverridesEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "http://test:8888");
            Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, "A=2,B=3");
            Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, "2000");

            var options = new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:200"),
                Headers = "C=3",
                TimeoutMilliseconds = 40000,
            };

            Assert.Equal(new Uri("http://localhost:200"), options.Endpoint);
            Assert.Equal("C=3", options.Headers);
            Assert.Equal(40000, options.TimeoutMilliseconds);
        }

        [Fact]
        public void OtlpExporterOptions_EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_EXPORTER_OTLP_ENDPOINT", OtlpExporterOptions.EndpointEnvVarName);
            Assert.Equal("OTEL_EXPORTER_OTLP_HEADERS", OtlpExporterOptions.HeadersEnvVarName);
            Assert.Equal("OTEL_EXPORTER_OTLP_TIMEOUT", OtlpExporterOptions.TimeoutEnvVarName);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, null);
            Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, null);
            Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, null);
        }
    }
}
