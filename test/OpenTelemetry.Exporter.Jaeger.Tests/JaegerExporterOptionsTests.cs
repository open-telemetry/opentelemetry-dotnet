// <copyright file="JaegerExporterOptionsTests.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class JaegerExporterOptionsTests : IDisposable
    {
        public JaegerExporterOptionsTests()
        {
            ClearEnvVars();
        }

        public void Dispose()
        {
            ClearEnvVars();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void JaegerExporterOptions_Defaults()
        {
            var options = new JaegerExporterOptions();

            Assert.Equal("localhost", options.AgentHost);
            Assert.Equal(6831, options.AgentPort);
            Assert.Equal(4096, options.MaxPayloadSizeInBytes);
            Assert.Equal(ExportProcessorType.Batch, options.ExportProcessorType);
            Assert.Equal(JaegerExportProtocol.UdpCompactThrift, options.Protocol);
            Assert.Equal(JaegerExporterOptions.DefaultJaegerEndpoint, options.Endpoint.ToString());
        }

        [Fact]
        public void JaegerExporterOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelAgentHostEnvVarKey, "jaeger-host");
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelAgentPortEnvVarKey, "123");
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelProtocolEnvVarKey, "http/thrift.binary");
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelEndpointEnvVarKey, "http://custom-endpoint:12345");

            var options = new JaegerExporterOptions();

            Assert.Equal("jaeger-host", options.AgentHost);
            Assert.Equal(123, options.AgentPort);
            Assert.Equal(JaegerExportProtocol.HttpBinaryThrift, options.Protocol);
            Assert.Equal(new Uri("http://custom-endpoint:12345"), options.Endpoint);
        }

        [Theory]
        [InlineData(JaegerExporterOptions.OTelAgentPortEnvVarKey)]
        [InlineData(JaegerExporterOptions.OTelProtocolEnvVarKey)]
        public void JaegerExporterOptions_InvalidEnvironmentVariableOverride(string envVar)
        {
            Environment.SetEnvironmentVariable(envVar, "invalid");

            Assert.Throws<FormatException>(() => new JaegerExporterOptions());
        }

        [Fact]
        public void JaegerExporterOptions_SetterOverridesEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelAgentHostEnvVarKey, "envvar-host");

            var options = new JaegerExporterOptions
            {
                AgentHost = "incode-host",
            };

            Assert.Equal("incode-host", options.AgentHost);
        }

        [Fact]
        public void JaegerExporterOptions_EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_EXPORTER_JAEGER_PROTOCOL", JaegerExporterOptions.OTelProtocolEnvVarKey);
            Assert.Equal("OTEL_EXPORTER_JAEGER_AGENT_HOST", JaegerExporterOptions.OTelAgentHostEnvVarKey);
            Assert.Equal("OTEL_EXPORTER_JAEGER_AGENT_PORT", JaegerExporterOptions.OTelAgentPortEnvVarKey);
            Assert.Equal("OTEL_EXPORTER_JAEGER_ENDPOINT", JaegerExporterOptions.OTelEndpointEnvVarKey);
        }

        [Fact]
        public void JaegerExporterOptions_FromConfigurationTest()
        {
            var values = new Dictionary<string, string>()
            {
                [JaegerExporterOptions.OTelProtocolEnvVarKey] = "http/thrift.binary",
                [JaegerExporterOptions.OTelAgentHostEnvVarKey] = "jaeger-host",
                [JaegerExporterOptions.OTelAgentPortEnvVarKey] = "123",
                [JaegerExporterOptions.OTelEndpointEnvVarKey] = "http://custom-endpoint:12345",
                ["OTEL_BSP_MAX_QUEUE_SIZE"] = "18",
                ["OTEL_BSP_MAX_EXPORT_BATCH_SIZE"] = "2",
                ["Jaeger:BatchExportProcessorOptions:MaxExportBatchSize"] = "5",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            IServiceCollection services = null;

            using var provider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(s =>
                {
                    services = s;
                    services.AddSingleton<IConfiguration>(configuration);
                    services.Configure<JaegerExporterOptions>(configuration.GetSection("Jaeger"));
                })
                .AddJaegerExporter()
                .Build();

            Assert.NotNull(services);

            using var serviceProvider = services.BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptionsMonitor<JaegerExporterOptions>>().CurrentValue;

            Assert.Equal("jaeger-host", options.AgentHost);
            Assert.Equal(123, options.AgentPort);
            Assert.Equal(JaegerExportProtocol.HttpBinaryThrift, options.Protocol);
            Assert.Equal(new Uri("http://custom-endpoint:12345"), options.Endpoint);
            Assert.Equal(18, options.BatchExportProcessorOptions.MaxQueueSize);

            // Note:
            //  1. OTEL_BSP_MAX_EXPORT_BATCH_SIZE is processed in BatchExportActivityProcessorOptions ctor and sets MaxExportBatchSize to 2.
            //  2. Jaeger:BatchExportProcessorOptions:MaxExportBatchSize is processed by options binder after ctor and sets MaxExportBatchSize to 5.
            Assert.Equal(5, options.BatchExportProcessorOptions.MaxExportBatchSize);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelProtocolEnvVarKey, null);
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelAgentHostEnvVarKey, null);
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelAgentPortEnvVarKey, null);
            Environment.SetEnvironmentVariable(JaegerExporterOptions.OTelEndpointEnvVarKey, null);
        }
    }
}
