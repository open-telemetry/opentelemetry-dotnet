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
            InitializeLoggerFactory(out OpenTelemetryLoggerProvider provider);

            foreach (var a in provider.GetResource().Attributes)
            {
                if (a.Key == "service.name")
                {
                    // NOTE: THIS TEST IS FAILING IN LINUX BUILDS ON GITHUB.
                    // I CHANGED THE TEST TO SEE WHAT THE ACTUAL VALUE IS AT RUNTIME
                    Assert.Equal("unknown_service:testhost", a.Value);
                }
            }

            Assert.Contains(provider.GetResource().Attributes, (kvp) => kvp.Key == "service.name" && kvp.Value.ToString().Contains("unknown_service"));
        }

        [Fact]
        public void VerifyResourceBuilderAddService()
        {
            InitializeLoggerFactory(out OpenTelemetryLoggerProvider provider, configure: options => options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "MyService", serviceVersion: "1.2.3")));

            Assert.Contains(provider.GetResource().Attributes, (kvp) => kvp.Key == "service.name" && kvp.Value.ToString() == "MyService");
            Assert.Contains(provider.GetResource().Attributes, (kvp) => kvp.Key == "service.version" && kvp.Value.ToString() == "1.2.3");
        }

        [Fact]
        public void VerifyResourceBuilder_WithServiceNameEnVar()
        {
            try
            {
                Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "MyService");

                InitializeLoggerFactory(out OpenTelemetryLoggerProvider provider);

                Assert.Contains(provider.GetResource().Attributes, (kvp) => kvp.Key == "service.name" && kvp.Value.ToString() == "MyService");
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

                InitializeLoggerFactory(out OpenTelemetryLoggerProvider provider);

                Assert.Contains(provider.GetResource().Attributes, (kvp) => kvp.Key == "Key1" && kvp.Value.ToString() == "Val1");
                Assert.Contains(provider.GetResource().Attributes, (kvp) => kvp.Key == "Key2" && kvp.Value.ToString() == "Val2");
            }
            finally
            {
                Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
            }
        }

        private static void InitializeLoggerFactory(out OpenTelemetryLoggerProvider provider, Action<OpenTelemetryLoggerOptions> configure = null)
        {
            var exporter = new InMemoryExporter<LogRecord>(new List<LogRecord>());
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

            provider = exporter.ParentProvider as OpenTelemetryLoggerProvider;
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
