// <copyright file="JaegerExporterTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class JaegerExporterOptionsTests : IDisposable
    {
        public JaegerExporterOptionsTests()
        {
            this.ClearEnvVars();
        }

        public void Dispose()
        {
            this.ClearEnvVars();
        }

        [Fact]
        public void JaegerExporterOptions_Defaults()
        {
            var options = new JaegerExporterOptions();

            Assert.Equal("localhost", options.AgentHost);
            Assert.Equal(6831, options.AgentPort);
            Assert.Equal(4096, options.MaxPayloadSizeInBytes);
            Assert.Equal(ExportProcessorType.Batch, options.ExportProcessorType);
        }

        [Fact]
        public void JaegerExporterOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_JAEGER_AGENT_HOST", "jeager-host");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_JAEGER_AGENT_PORT", "123");

            var options = new JaegerExporterOptions();

            Assert.Equal("jeager-host", options.AgentHost);
            Assert.Equal(123, options.AgentPort);
        }

        [Fact]
        public void JaegerExporterOptions_InvalidPortEnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_JAEGER_AGENT_PORT", "invalid");

            var options = new JaegerExporterOptions();

            Assert.Equal(6831, options.AgentPort); // use default
        }

        private void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_JAEGER_AGENT_HOST", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_JAEGER_AGENT_PORT", null);
        }

    }
}
