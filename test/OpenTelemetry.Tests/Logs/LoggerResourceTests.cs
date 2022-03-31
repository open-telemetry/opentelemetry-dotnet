// <copyright file="LoggerResourceTests.cs" company="OpenTelemetry Authors">
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
#if !NET461

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LoggerResourceTests
    {
        [Fact]
        public void VerifyResaurceBuilder_DefaultBehavior()
        {
            VerifyResourceBuilder(
                configure: null,
                assert: (Resource resource) =>
                {
                    // Note: actual value may vary depending on test runner. Visual Studio: "unknown_service:testhost". Dotnet CLI: "unknown_service:dotnet"
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service"));
                });
        }

        [Fact]
        public void VerifyResourceBuilder_WithAddService()
        {
            VerifyResourceBuilder(
                configure: options => options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "MyService", serviceVersion: "1.2.3")),
                assert: (Resource resource) =>
                {
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString() == "MyService");
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceVersion && kvp.Value.ToString() == "1.2.3");
                });
        }

        [Fact]
        public void VerifyResourceBuilder_WithServiceNameEnVar()
        {
            VerifyResourceBuilder(
                environmentVariableName: OtelServiceNameEnvVarDetector.EnvVarKey,
                environmentVariableValue: "MyService",
                configure: null,
                assert: (Resource resource) =>
                {
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString() == "MyService");
                });
        }

        [Fact]
        public void VerifyResourceBuilder_WithAttributesEnVar()
        {
            VerifyResourceBuilder(
                environmentVariableName: OtelEnvResourceDetector.EnvVarKey,
                environmentVariableValue: "Key1=Val1,Key2=Val2",
                configure: null,
                assert: (Resource resource) =>
                {
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == "Key1" && kvp.Value.ToString() == "Val1");
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == "Key2" && kvp.Value.ToString() == "Val2");
                });
        }

        private static void VerifyResourceBuilder(
            string environmentVariableName,
            string environmentVariableValue,
            Action<OpenTelemetryLoggerOptions> configure,
            Action<Resource> assert)
        {
            try
            {
                Environment.SetEnvironmentVariable(environmentVariableName, environmentVariableValue);

                VerifyResourceBuilder(configure, assert);
            }
            finally
            {
                Environment.SetEnvironmentVariable(environmentVariableName, null);
            }
        }

        private static void VerifyResourceBuilder(
            Action<OpenTelemetryLoggerOptions> configure,
            Action<Resource> assert)
        {
            // Setup
            using var exporter = new InMemoryExporter<LogRecord>(new List<LogRecord>());
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    configure?.Invoke(options);
                    options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
                });
            });
            var logger = loggerFactory.CreateLogger<LoggerResourceTests>();

            var provider = exporter.ParentProvider as OpenTelemetryLoggerProvider;
            Assert.NotNull(provider);
            var resource = provider.GetResource();
            Assert.NotNull(resource);

            // Verify
            assert.Invoke(resource);
        }
    }
}
#endif
