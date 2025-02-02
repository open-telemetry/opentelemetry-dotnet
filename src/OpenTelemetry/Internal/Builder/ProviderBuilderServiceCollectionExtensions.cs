// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

internal static class ProviderBuilderServiceCollectionExtensions
{
    public static IServiceCollection AddOpenTelemetryLoggerProviderBuilderServices(this IServiceCollection services)
    {
        services.TryAddSingleton<LoggerProviderBuilderSdk>();
        services.RegisterOptionsFactory(configuration => new BatchExportLogRecordProcessorOptions(configuration));
        services.RegisterOptionsFactory(
            (sp, configuration, name) => new LogRecordExportProcessorOptions(
                sp.GetRequiredService<IOptionsMonitor<BatchExportLogRecordProcessorOptions>>().Get(name)));

        return services;
    }

    public static IServiceCollection AddOpenTelemetryMeterProviderBuilderServices(this IServiceCollection services)
    {
        services.TryAddSingleton<MeterProviderBuilderSdk>();
        services.RegisterOptionsFactory(configuration => new PeriodicExportingMetricReaderOptions(configuration));
        services.RegisterOptionsFactory(
            (sp, configuration, name) => new MetricReaderOptions(
                sp.GetRequiredService<IOptionsMonitor<PeriodicExportingMetricReaderOptions>>().Get(name)));

        return services;
    }

    public static IServiceCollection AddOpenTelemetryTracerProviderBuilderServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TracerProviderBuilderSdk>();
        services.RegisterOptionsFactory(configuration => new BatchExportActivityProcessorOptions(configuration));
        services.RegisterOptionsFactory(
            (sp, configuration, name) => new ActivityExportProcessorOptions(
                sp.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name)));

        return services;
    }

    public static IServiceCollection AddOpenTelemetrySharedProviderBuilderServices(this IServiceCollection services)
    {
        // Accessing Sdk class is just to trigger its static ctor,
        // which sets default Propagators and default Activity Id format
        _ = Sdk.SuppressInstrumentation;

        services.AddOptions();

        // Note: When using a host builder IConfiguration is automatically
        // registered and this registration will no-op. This only runs for
        // Sdk.Create* style or when manually creating a ServiceCollection. The
        // point of this registration is to make IConfiguration available in
        // those cases.
        services.TryAddSingleton<IConfiguration>(
            sp => new ConfigurationBuilder().AddEnvironmentVariables().Build());

        return services;
    }
}
