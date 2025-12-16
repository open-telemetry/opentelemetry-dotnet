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
        Debug.Assert(services != null, "services was null");

        AddOtlpExporterSharedServices(services!, registerSdkLimitOptions: true);
    }

    public static void AddOtlpExporterMetricsServices(this IServiceCollection services, string name)
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(name != null, "name was null");

        AddOtlpExporterSharedServices(services!, registerSdkLimitOptions: false);

        services!.AddOptions<MetricReaderOptions>(name).Configure<IConfiguration>(
            (readerOptions, config) =>
            {
                var otlpTemporalityPreference = config[OtlpSpecConfigDefinitions.MetricsTemporalityPreferenceEnvVarName];
                if (!string.IsNullOrWhiteSpace(otlpTemporalityPreference)
                    && Enum.TryParse<MetricReaderTemporalityPreference>(otlpTemporalityPreference, ignoreCase: true, out var enumValue))
                {
                    readerOptions.TemporalityPreference = enumValue;
                }

                // Parse histogram aggregation using direct string comparison instead of Enum.TryParse.
                // The spec defines snake_case values (explicit_bucket_histogram, base2_exponential_bucket_histogram).
                // Using direct string comparison ensures we strictly validate against spec-defined values and fail
                // gracefully for invalid inputs, rather than attempting to parse arbitrary strings to enum values.
                var otlpDefaultHistogramAggregation = config[OtlpSpecConfigDefinitions.MetricsDefaultHistogramAggregationEnvVarName];
                if (!string.IsNullOrWhiteSpace(otlpDefaultHistogramAggregation))
                {
                    if (otlpDefaultHistogramAggregation!.Equals("base2_exponential_bucket_histogram", StringComparison.OrdinalIgnoreCase))
                    {
                        readerOptions.DefaultHistogramAggregation = MetricReaderHistogramAggregation.Base2ExponentialBucketHistogram;
                    }
                    else if (otlpDefaultHistogramAggregation.Equals("explicit_bucket_histogram", StringComparison.OrdinalIgnoreCase))
                    {
                        readerOptions.DefaultHistogramAggregation = MetricReaderHistogramAggregation.ExplicitBucketHistogram;
                    }
                }
            });
    }

    public static void AddOtlpExporterTracingServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null");

        AddOtlpExporterSharedServices(services!, registerSdkLimitOptions: true);
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
