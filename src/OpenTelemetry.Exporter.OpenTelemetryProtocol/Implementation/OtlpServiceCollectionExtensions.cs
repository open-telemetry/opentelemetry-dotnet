// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

internal static class OtlpServiceCollectionExtensions
{
    private const string OtlpLogExporterHttpClientCategory = "System.Net.Http.HttpClient.OtlpLogExporter";
    private const string OtlpMetricExporterHttpClientCategory = "System.Net.Http.HttpClient.OtlpMetricExporter";
    private const string OtlpTraceExporterHttpClientCategory = "System.Net.Http.HttpClient.OtlpTraceExporter";

    public static void AddOtlpExporterLoggingServices(this IServiceCollection services)
        => AddOtlpExporterSharedServices(
            services,
            registerSdkLimitOptions: true,
            OtlpLogExporterHttpClientCategory);

    public static void AddOtlpExporterMetricsServices(this IServiceCollection services, string name)
    {
        AddOtlpExporterSharedServices(
            services,
            registerSdkLimitOptions: false,
            OtlpMetricExporterHttpClientCategory);

        services.AddOptions<MetricReaderOptions>(name).Configure<IConfiguration>(
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
                // Case-insensitive comparison is used for flexibility, though the spec uses lowercase.
                var otlpDefaultHistogramAggregation = config[OtlpSpecConfigDefinitions.MetricsDefaultHistogramAggregationEnvVarName];
                if (string.Equals(otlpDefaultHistogramAggregation, "base2_exponential_bucket_histogram", StringComparison.OrdinalIgnoreCase))
                {
                    readerOptions.DefaultHistogramAggregation = MetricReaderHistogramAggregation.Base2ExponentialBucketHistogram;
                }
                else if (string.Equals(otlpDefaultHistogramAggregation, "explicit_bucket_histogram", StringComparison.OrdinalIgnoreCase))
                {
                    readerOptions.DefaultHistogramAggregation = MetricReaderHistogramAggregation.ExplicitBucketHistogram;
                }
            });
    }

    public static void AddOtlpExporterTracingServices(this IServiceCollection services)
        => AddOtlpExporterSharedServices(
            services,
            registerSdkLimitOptions: true,
            OtlpTraceExporterHttpClientCategory);

    private static void AddOtlpExporterSharedServices(
        IServiceCollection services,
        bool registerSdkLimitOptions,
        string httpClientCategoryName)
    {
        services.Configure<LoggerFilterOptions>(loggerFilterOptions =>
        {
            AddOtlpHttpClientLoggerFilter(loggerFilterOptions, httpClientCategoryName);
        });

        services.RegisterOptionsFactory(OtlpExporterOptions.CreateOtlpExporterOptions);
        services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));

        if (registerSdkLimitOptions)
        {
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        }
    }

    private static void AddOtlpHttpClientLoggerFilter(LoggerFilterOptions loggerFilterOptions, string categoryName)
    {
        if (!loggerFilterOptions.Rules.Any(rule =>
            rule.ProviderName == null
            && string.Equals(rule.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase)))
        {
            loggerFilterOptions.Rules.Add(new LoggerFilterRule(
                providerName: null,
                categoryName,
                LogLevel.Warning,
                filter: null));
        }
    }
}
