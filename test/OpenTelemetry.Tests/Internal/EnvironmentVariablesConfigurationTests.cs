// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Security;
#if NETFRAMEWORK
using System.Security.Permissions;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public class EnvironmentVariablesConfigurationTests
{
    [Theory]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES")]
    [InlineData("OTEL_SERVICE_NAME")]
    public void ResourceDetection_WhenConfigurationAccessDenied_PreservesOtherAttributes(string deniedKey)
    {
        var configuration = new ConfigurationBuilder()
            .Add(new DeniedConfigurationSource(deniedKey, new SecurityException()))
            .Build();
        using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
        var builder = ResourceBuilder.CreateEmpty()
            .AddAttributes([new("test.attribute", "retained")])
            .AddEnvironmentVariableDetector();
        builder.ServiceProvider = services;

        var resource = builder.Build();

        Assert.Contains(new KeyValuePair<string, object>("test.attribute", "retained"), resource.Attributes);
        Assert.Contains(
            deniedKey == "OTEL_RESOURCE_ATTRIBUTES"
                ? new KeyValuePair<string, object>("service.name", "configured-service")
                : new KeyValuePair<string, object>("configuration.attribute", "retained"),
            resource.Attributes);
    }

    [Fact]
    public void ResourceDetection_WhenConfigurationFails_PropagatesOtherExceptions()
    {
        var error = new InvalidOperationException("Configuration failed.");
        var configuration = new ConfigurationBuilder()
            .Add(new DeniedConfigurationSource("OTEL_RESOURCE_ATTRIBUTES", error))
            .Build();
        using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
        var builder = ResourceBuilder.CreateEmpty().AddEnvironmentVariableDetector();
        builder.ServiceProvider = services;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => builder.Build()));
    }

    [Theory]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES")]
    [InlineData("OTEL_SERVICE_NAME")]
    public void ResourceDetector_WhenConfigurationAccessDenied_ReturnsEmptyResource(string deniedKey)
    {
        var configuration = new ConfigurationBuilder()
            .Add(new DeniedConfigurationSource(deniedKey, new SecurityException()))
            .Build();
        IResourceDetector detector = deniedKey == "OTEL_SERVICE_NAME"
            ? new OtelServiceNameEnvVarDetector(configuration)
            : new OtelEnvResourceDetector(configuration);

        Assert.Empty(detector.Detect().Attributes);
    }

    [Fact]
    public void ProcessorOptions_WhenConfigurationAccessDenied_UseDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .Add(new DeniedConfigurationSource(deniedKey: null, new SecurityException()))
            .Build();

        var traceOptions = new BatchExportActivityProcessorOptions(configuration);
        var logOptions = new BatchExportLogRecordProcessorOptions(configuration);
        var metricOptions = new PeriodicExportingMetricReaderOptions(configuration);

        Assert.Equal(2048, traceOptions.MaxQueueSize);
        Assert.Equal(512, traceOptions.MaxExportBatchSize);
        Assert.Equal(2048, logOptions.MaxQueueSize);
        Assert.Equal(512, logOptions.MaxExportBatchSize);
        Assert.Null(metricOptions.ExportIntervalMilliseconds);
        Assert.Null(metricOptions.ExportTimeoutMilliseconds);
    }

#if NETFRAMEWORK
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnvironmentConfiguration_WhenReloadAccessDenied_RetainsValuesAndCanRecover(bool initiallyDenied)
    {
        var key = $"OTEL_TEST_PERMISSION_{Guid.NewGuid():N}";
        using var environmentScope = EnvironmentVariableScope.Create(key, "before");
        IConfigurationRoot configuration;
        if (initiallyDenied)
        {
            new EnvironmentPermission(PermissionState.None).PermitOnly();
            try
            {
                configuration = EnvironmentVariablesConfiguration.Create();
            }
            finally
            {
                CodeAccessPermission.RevertPermitOnly();
            }
        }
        else
        {
            configuration = EnvironmentVariablesConfiguration.Create();
        }

        using var configurationLifetime = (IDisposable)configuration;
        Environment.SetEnvironmentVariable(key, "after");

        string? cachedValue;
        new EnvironmentPermission(PermissionState.None).PermitOnly();
        try
        {
            // The standard provider reads cached values until Load is called again by Reload.
            cachedValue = configuration[key];
            configuration.Reload();
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        Assert.Equal(initiallyDenied ? null : "before", cachedValue);
        Assert.Equal(initiallyDenied ? null : "before", configuration[key]);

        configuration.Reload();

        Assert.Equal("after", configuration[key]);
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("traces")]
    [InlineData("logs")]
    [InlineData("metrics")]
    [InlineData("services")]
    public void DefaultConfiguration_WhenEnvironmentAccessDenied_UsesDefaults(string component)
    {
        // PermitOnly limits this call stack; it does not change process-wide environment variables.
        using var services = new ServiceCollection()
            .AddOpenTelemetrySharedProviderBuilderServices()
            .BuildServiceProvider();
        object result;
        new EnvironmentPermission(PermissionState.None).PermitOnly();
        try
        {
            result = component switch
            {
                "resource" => ResourceBuilder.CreateEmpty().AddEnvironmentVariableDetector().Build(),
                "traces" => new BatchExportActivityProcessorOptions(),
                "logs" => new BatchExportLogRecordProcessorOptions(),
                "metrics" => new PeriodicExportingMetricReaderOptions(),
                "services" => services.GetRequiredService<IConfiguration>(),
                _ => throw new InvalidOperationException(component),
            };
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        switch (result)
        {
            case Resource resource:
                Assert.Empty(resource.Attributes);
                break;
            case BatchExportActivityProcessorOptions options:
                Assert.Equal(2048, options.MaxQueueSize);
                break;
            case BatchExportLogRecordProcessorOptions options:
                Assert.Equal(2048, options.MaxQueueSize);
                break;
            case PeriodicExportingMetricReaderOptions options:
                Assert.Null(options.ExportIntervalMilliseconds);
                break;
            case IConfiguration configuration:
                Assert.Empty(configuration.AsEnumerable());
                break;
        }
    }
#endif

    private sealed class DeniedConfigurationSource(string? deniedKey, Exception error) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new DeniedConfigurationProvider(deniedKey, error);
    }

    private sealed class DeniedConfigurationProvider : ConfigurationProvider
    {
        private readonly string? deniedKey;
        private readonly Exception error;

        public DeniedConfigurationProvider(string? deniedKey, Exception error)
        {
            this.deniedKey = deniedKey;
            this.error = error;
            this.Data["OTEL_SERVICE_NAME"] = "configured-service";
            this.Data["OTEL_RESOURCE_ATTRIBUTES"] = "configuration.attribute=retained";
        }

        public override bool TryGet(string key, out string? value)
        {
            if (this.deniedKey == null || string.Equals(this.deniedKey, key, StringComparison.Ordinal))
            {
                throw this.error;
            }

            return base.TryGet(key, out value);
        }
    }
}
