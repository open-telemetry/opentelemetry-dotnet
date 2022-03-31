// <copyright file="LoggerFactoryAndResourceBuilderTests.cs" company="OpenTelemetry Authors">
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
    public sealed class LoggerFactoryAndResourceBuilderTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData(OtelServiceNameEnvVarDetector.EnvVarKey, "MyService")]
        public void VerifyResourceBuilderReadsEnvironmentVariable(string name, string value)
        {
            bool defaultCase = name == null && value == null;

            try
            {
                // Pre-Setup
                if (!defaultCase)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }

                // Setup
                using var exporter = new InMemoryExporter<LogRecord>(new List<LogRecord>());
                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddOpenTelemetry(options =>
                    {
                        options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
                    });
                });
                var logger = loggerFactory.CreateLogger<LoggerFactoryAndResourceBuilderTests>();

                var provider = exporter.ParentProvider as OpenTelemetryLoggerProvider;
                Assert.NotNull(provider);
                var resource = provider.GetResource();
                Assert.NotNull(resource);

                // Verify
                Assert.Contains(resource.Attributes, (kvp) =>
                    kvp.Key == ResourceSemanticConventions.AttributeServiceName
                    && defaultCase ? kvp.Value.ToString().Contains("unknown_service") : kvp.Value.Equals(value));
            }
            finally
            {
                // Cleanup
                if (!defaultCase)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }
            }
        }
    }
}
#endif
