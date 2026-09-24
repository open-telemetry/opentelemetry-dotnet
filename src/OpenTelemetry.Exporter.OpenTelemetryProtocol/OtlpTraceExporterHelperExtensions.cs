// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpTraceExporterHelperExtensions
{
    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder)
        => AddOtlpExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions> configure)
        => AddOtlpExporter(builder, name: null, configure);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporter(
        this TracerProviderBuilder builder,
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

            services.AddOtlpExporterTracingServices();
        });

        return builder.AddProcessor(sp =>
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

            // Note: Not using finalOptionsName here for SdkLimitOptions.
            // There should only be one provider for a given service
            // collection so SdkLimitOptions is treated as a single default
            // instance.
            var sdkLimitOptions = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildOtlpExporterProcessor(
                sp,
                exporterOptions,
                sdkLimitOptions,
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName),
                optionsName: name);
        });
    }

    internal static BaseProcessor<Activity> BuildOtlpExporterProcessor(
        IServiceProvider serviceProvider,
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        Func<BaseExporter<Activity>, BaseExporter<Activity>>? configureExporterInstance = null,
        string? optionsName = null)
        => BuildOtlpExporterProcessor(
            serviceProvider,
            exporterOptions,
            sdkLimitOptions,
            experimentalOptions,
            exporterOptions.ExportProcessorType,
            exporterOptions.BatchExportProcessorOptions ?? new BatchExportActivityProcessorOptions(),
            skipUseOtlpExporterRegistrationCheck: false,
            configureExporterInstance: configureExporterInstance,
            optionsName: optionsName);

    internal static BaseProcessor<Activity> BuildOtlpExporterProcessor(
        IServiceProvider serviceProvider,
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        ExportProcessorType exportProcessorType,
        BatchExportProcessorOptions<Activity> batchExportProcessorOptions,
        bool skipUseOtlpExporterRegistrationCheck = false,
        Func<BaseExporter<Activity>, BaseExporter<Activity>>? configureExporterInstance = null,
        string? optionsName = null)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        if (exporterOptions.Protocol == OtlpExportProtocol.Grpc &&
            ReferenceEquals(exporterOptions.HttpClientFactory, exporterOptions.DefaultHttpClientFactory))
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
        {
            throw new NotSupportedException("OtlpExportProtocol.Grpc with the default HTTP client factory is not supported on .NET Framework or .NET Standard 2.0." +
                "Please switch to OtlpExportProtocol.HttpProtobuf or provide a custom HttpClientFactory.");
        }
#endif

        if (!skipUseOtlpExporterRegistrationCheck)
        {
            serviceProvider.EnsureNoUseOtlpExporterRegistrations();
        }

        var usesHttpClientFactory = exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");

        OtlpExporterTransmissionHandler? transmissionHandler = null;
        ReloadableExportClient? reloadableClient = null;
        if (optionsName != null)
        {
            var ownsHttpClient = usesHttpClientFactory || ReferenceEquals(exporterOptions.HttpClientFactory, exporterOptions.DefaultHttpClientFactory);
#pragma warning disable CA2000 // Ownership passes to the exporter.
            reloadableClient = new ReloadableExportClient(
                exporterOptions,
                exporterOptions.GetExportClient(OtlpSignalType.Traces, ownsHttpClient),
                ownsHttpClient,
                serviceProvider,
                optionsName,
                "OtlpTraceExporter",
                OtlpSignalType.Traces,
                skipUseOtlpExporterRegistrationCheck);
            transmissionHandler = exporterOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces, exportClientOverride: reloadableClient);
#pragma warning restore CA2000 // Ownership passes to the exporter.
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        BaseExporter<Activity> otlpExporter = reloadableClient is null
            ? new OtlpTraceExporter(exporterOptions, sdkLimitOptions, experimentalOptions)
            : new ReloadableOtlpTraceExporter(exporterOptions, sdkLimitOptions, experimentalOptions, transmissionHandler!, reloadableClient);
#pragma warning restore CA2000 // Dispose objects before losing scope

        try
        {
            if (configureExporterInstance != null)
            {
                otlpExporter = configureExporterInstance(otlpExporter);
            }

            return exportProcessorType == ExportProcessorType.Simple
                ? new SimpleActivityExportProcessor(otlpExporter)
                : new BatchActivityExportProcessor(
                    otlpExporter,
                    batchExportProcessorOptions.MaxQueueSize,
                    batchExportProcessorOptions.ScheduledDelayMilliseconds,
                    batchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    batchExportProcessorOptions.MaxExportBatchSize);
        }
        catch
        {
            otlpExporter.Dispose();
            throw;
        }
    }
}
