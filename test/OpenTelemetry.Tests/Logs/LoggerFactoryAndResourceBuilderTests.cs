// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
            assert: resource =>
            {
                Assert.Contains(
                    resource.Attributes,
                    kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName &&
#if NET
                           kvp.Value.ToString()!.Contains("unknown_service", StringComparison.Ordinal));
#else
                           kvp.Value.ToString()!.Contains("unknown_service"));
#endif
            });
    }

    [Fact]
    public void VerifyResourceBuilder_WithServiceNameEnVar()
    {
        try
        {
            Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "MyService");

            VerifyResourceBuilder(
                assert: resource =>
                {
                    Assert.Contains(resource.Attributes, kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.Equals("MyService"));
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
