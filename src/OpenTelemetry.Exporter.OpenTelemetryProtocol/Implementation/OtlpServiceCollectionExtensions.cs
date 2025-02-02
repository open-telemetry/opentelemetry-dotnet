// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

internal static class OtlpServiceCollectionExtensions
{
    public static void AddOtlpExporterLoggingServices(this IServiceCollection services)
    {
        AddOtlpExporterSharedServices(services, registerSdkLimitOptions: true);
    }

    public static void AddOtlpExporterMetricsServices(this IServiceCollection services, string name)
    {
        AddOtlpExporterSharedServices(services, registerSdkLimitOptions: false);

        services.AddOptions<MetricReaderOptions>(name).Configure<IConfiguration>(
            (readerOptions, config) =>
            {
                var otlpTemporalityPreference = config[OtlpSpecConfigDefinitions.MetricsTemporalityPreferenceEnvVarName];
                if (!string.IsNullOrWhiteSpace(otlpTemporalityPreference)
                    && Enum.TryParse<MetricReaderTemporalityPreference>(otlpTemporalityPreference, ignoreCase: true, out var enumValue))
                {
                    readerOptions.TemporalityPreference = enumValue;
                }
            });
    }

    public static void AddOtlpExporterTracingServices(this IServiceCollection services)
    {
        AddOtlpExporterSharedServices(services, registerSdkLimitOptions: true);
    }

    private static void AddOtlpExporterSharedServices(
        IServiceCollection services,
        bool registerSdkLimitOptions)
    {
        services.RegisterOptionsFactory(OtlpExporterOptions.CreateOtlpExporterOptions);
        services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));

        if (registerSdkLimitOptions)
        {
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        }
    }
}
