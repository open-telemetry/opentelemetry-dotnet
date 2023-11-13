// <copyright file="OtlpMetricExporterExtensions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpMetricExporterExtensions
{
    internal const string OtlpMetricExporterTemporalityPreferenceEnvVarKey = "OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE";

    /// <summary>
    /// Adds <see cref="OtlpMetricExporter"/> to the <see cref="MeterProviderBuilder"/> using default options.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddOtlpExporter(this MeterProviderBuilder builder)
        => AddOtlpExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds <see cref="OtlpMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddOtlpExporter(this MeterProviderBuilder builder, Action<OtlpExporterOptions> configure)
        => AddOtlpExporter(builder, name: null, configure);

    /// <summary>
    /// Adds <see cref="OtlpMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddOtlpExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<OtlpExporterOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (name != null && configure != null)
            {
                // If we are using named options we register the
                // configuration delegate into options pipeline.
                services.Configure(finalOptionsName, configure);
            }

            OtlpExporterOptions.RegisterOtlpExporterOptionsFactory(services);

            services.AddOptions<MetricReaderOptions>(finalOptionsName).Configure<IConfiguration>(
                (readerOptions, config) =>
                {
                    var otlpTemporalityPreference = config[OtlpMetricExporterTemporalityPreferenceEnvVarKey];
                    if (!string.IsNullOrWhiteSpace(otlpTemporalityPreference)
                        && Enum.TryParse<MetricReaderTemporalityPreference>(otlpTemporalityPreference, ignoreCase: true, out var enumValue))
                    {
                        readerOptions.TemporalityPreference = enumValue;
                    }
                });
        });

        return builder.AddReader(sp =>
        {
            OtlpExporterOptions exporterOptions;

            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // OtlpExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together. See:
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/4043
                exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>().Create(finalOptionsName);

                // Configuration delegate is executed inline on the fresh instance.
                configure?.Invoke(exporterOptions);
            }
            else
            {
                // When using named options we can properly utilize Options
                // API to create or reuse an instance.
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
            }

            return BuildOtlpExporterMetricReader(
                exporterOptions,
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName),
                sp);
        });
    }

    /// <summary>
    /// Adds <see cref="OtlpMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporterAndMetricReader">Callback action for
    /// configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddOtlpExporter(
        this MeterProviderBuilder builder,
        Action<OtlpExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
        => AddOtlpExporter(builder, name: null, configureExporterAndMetricReader);

    /// <summary>
    /// Adds <see cref="OtlpMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporterAndMetricReader">Optional callback action
    /// for configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddOtlpExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<OtlpExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            OtlpExporterOptions.RegisterOtlpExporterOptionsFactory(services);

            services.AddOptions<MetricReaderOptions>(name).Configure<IConfiguration>(
                (readerOptions, config) =>
                {
                    var otlpTemporalityPreference = config[OtlpMetricExporterTemporalityPreferenceEnvVarKey];
                    if (!string.IsNullOrWhiteSpace(otlpTemporalityPreference)
                        && Enum.TryParse<MetricReaderTemporalityPreference>(otlpTemporalityPreference, ignoreCase: true, out var enumValue))
                    {
                        readerOptions.TemporalityPreference = enumValue;
                    }
                });
        });

        return builder.AddReader(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(name);
            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildOtlpExporterMetricReader(exporterOptions, metricReaderOptions, sp);
        });
    }

    internal static MetricReader BuildOtlpExporterMetricReader(
        OtlpExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions,
        IServiceProvider serviceProvider,
        Func<BaseExporter<Metric>, BaseExporter<Metric>>? configureExporterInstance = null)
    {
        exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpMetricExporter");

        BaseExporter<Metric> metricExporter = new OtlpMetricExporter(exporterOptions);

        if (configureExporterInstance != null)
        {
            metricExporter = configureExporterInstance(metricExporter);
        }

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions);
    }
}
