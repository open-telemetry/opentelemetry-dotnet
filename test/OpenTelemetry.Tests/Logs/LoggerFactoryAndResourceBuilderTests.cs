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

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerFactoryAndResourceBuilderTests
{
    [Fact]
    public void TestLogExporterCanAccessResource()
    {
        VerifyResourceBuilder(
            assert: (Resource resource) =>
            {
                Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service"));
            });
    }

    [Fact]
    public void VerifyResourceBuilder_WithServiceNameEnVar()
    {
        try
        {
            Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "MyService");

            VerifyResourceBuilder(
                assert: (Resource resource) =>
                {
                    Assert.Contains(resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.Equals("MyService"));
                });
        }
        finally
        {
            Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
        }
    }

    private static void VerifyResourceBuilder(
        Action<Resource> assert)
    {
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

        Assert.NotNull(exporter.ParentProvider);

        var resource = exporter.ParentProvider.GetResource();
        Assert.NotNull(resource);

        // Verify
        assert.Invoke(resource);
    }
}
