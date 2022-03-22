// <copyright file="LoggerProviderTests.cs" company="OpenTelemetry Authors">
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
    public sealed class LoggerProviderTests
    {
        [Fact]
        public void VerifyDefaultBehavior()
        {
            var resource = CreateLoggerFactoryAndGetResource();

            // Note: actual value may vary depending on test runner. Visual Studio: "unknown_service:testhost". Dotnet CLI: "unknown_service:dotnet"
            Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service"));
        }

        [Fact]
        public void VerifyResourceBuilderAddService()
        {
            var resource = CreateLoggerFactoryAndGetResource(options => options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "MyService", serviceVersion: "1.2.3")));

            Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString() == "MyService");
            Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceVersion && kvp.Value.ToString() == "1.2.3");
        }

        [Fact]
        public void VerifyResourceBuilder_WithServiceNameEnVar()
        {
            try
            {
                Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "MyService");

                var resource = CreateLoggerFactoryAndGetResource();

                Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString() == "MyService");
            }
            finally
            {
                Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
            }
        }

        [Fact]
        public void VerifyResourceBuilder_WithAttributesEnVar()
        {
            try
            {
                Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, "Key1=Val1,Key2=Val2");

                var resource = CreateLoggerFactoryAndGetResource();

                Assert.Contains(resource.Attributes, (kvp) => kvp.Key == "Key1" && kvp.Value.ToString() == "Val1");
                Assert.Contains(resource.Attributes, (kvp) => kvp.Key == "Key2" && kvp.Value.ToString() == "Val2");
            }
            finally
            {
                Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
            }
        }

        private static Resource CreateLoggerFactoryAndGetResource(Action<OpenTelemetryLoggerOptions> configure = null)
        {
            using var exporter = new InMemoryExporter<LogRecord>(new List<LogRecord>());
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    configure?.Invoke(options);
                    options.AddProcessor(new TestLogRecordProcessor(exporter));
                });
                builder.AddFilter(typeof(LoggerProviderTests).FullName, LogLevel.Trace);
            });
            var logger = loggerFactory.CreateLogger<LoggerProviderTests>();

            using var provider = exporter.ParentProvider as OpenTelemetryLoggerProvider;
            return provider.GetResource();
        }

        private class TestLogRecordProcessor : SimpleExportProcessor<LogRecord>
        {
            public TestLogRecordProcessor(BaseExporter<LogRecord> exporter)
                : base(exporter)
            {
            }

            public override void OnEnd(LogRecord data)
            {
                data.BufferLogScopes();

                base.OnEnd(data);
            }
        }
    }
}
#endif
