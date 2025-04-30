// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpMetricExporterExtensions
{
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

            services.AddOtlpExporterMetricsServices(finalOptionsName);
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
                sp,
                exporterOptions,
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName),
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName));
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

        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            services.AddOtlpExporterMetricsServices(finalOptionsName);
        });

        return builder.AddReader(sp =>
        {
            OtlpExporterOptions exporterOptions;
            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // OtlpExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together.
                exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>().Create(finalOptionsName);
            }
            else
            {
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
            }

            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName);

            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildOtlpExporterMetricReader(
                sp,
                exporterOptions,
                metricReaderOptions,
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName));
        });
    }

    internal static MetricReader BuildOtlpExporterMetricReader(
        IServiceProvider serviceProvider,
        OtlpExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions,
        ExperimentalOptions experimentalOptions,
        bool skipUseOtlpExporterRegistrationCheck = false,
        Func<BaseExporter<Metric>, BaseExporter<Metric>>? configureExporterInstance = null)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(metricReaderOptions != null, "metricReaderOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

#if NET462_OR_GREATER || NETSTANDARD2_0
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        if (exporterOptions!.Protocol == OtlpExportProtocol.Grpc &&
            ReferenceEquals(exporterOptions.HttpClientFactory, exporterOptions.DefaultHttpClientFactory))
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
        {
            throw new NotSupportedException("OtlpExportProtocol.Grpc with the default HTTP client factory is not supported on .NET Framework or .NET Standard 2.0." +
                "Please switch to OtlpExportProtocol.HttpProtobuf or provide a custom HttpClientFactory.");
        }
#endif

        if (!skipUseOtlpExporterRegistrationCheck)
        {
            serviceProvider!.EnsureNoUseOtlpExporterRegistrations();
        }

        exporterOptions!.TryEnableIHttpClientFactoryIntegration(serviceProvider!, "OtlpMetricExporter");

#pragma warning disable CA2000 // Dispose objects before losing scope
        BaseExporter<Metric> metricExporter = new OtlpMetricExporter(exporterOptions!, experimentalOptions!);
#pragma warning restore CA2000 // Dispose objects before losing scope

        if (configureExporterInstance != null)
        {
            metricExporter = configureExporterInstance(metricExporter);
        }

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions!);
    }
}
