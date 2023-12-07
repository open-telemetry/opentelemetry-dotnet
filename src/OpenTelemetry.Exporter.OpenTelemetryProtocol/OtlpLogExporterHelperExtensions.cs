// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpLogExporterHelperExtensions
{
    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOtlpExporter(OpenTelemetryLoggerOptions, Action{OtlpExporterOptions})" path="/remarks"/></remarks>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
        => AddOtlpExporter(loggerOptions, name: null, configure: null);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions> configure)
        => AddOtlpExporter(loggerOptions, name: null, configure);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        string? name,
        Action<OtlpExporterOptions>? configure)
    {
        Guard.ThrowIfNull(loggerOptions);

        var finalOptionsName = name ?? Options.DefaultName;

        return loggerOptions.AddProcessor(sp =>
        {
            var exporterOptions = GetOtlpExporterOptions(sp, name, finalOptionsName);

            var processorOptions = sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName);

            configure?.Invoke(exporterOptions);

            return BuildOtlpLogExporter(sp, exporterOptions, processorOptions);
        });
    }

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configureExporterAndProcessor">Callback action for configuring <see cref="OtlpExporterOptions"/> and <see cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions> configureExporterAndProcessor)
        => AddOtlpExporter(loggerOptions, name: null, configureExporterAndProcessor);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporterAndProcessor">Optional callback action for configuring <see cref="OtlpExporterOptions"/> and <see cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        string? name,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions>? configureExporterAndProcessor)
    {
        Guard.ThrowIfNull(loggerOptions);

        var finalOptionsName = name ?? Options.DefaultName;

        return loggerOptions.AddProcessor(sp =>
        {
            var exporterOptions = GetOtlpExporterOptions(sp, name, finalOptionsName);

            var processorOptions = sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName);

            configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);

            return BuildOtlpLogExporter(sp, exporterOptions, processorOptions);
        });
    }

    internal static BaseProcessor<LogRecord> BuildOtlpLogExporter(
        IServiceProvider sp,
        OtlpExporterOptions exporterOptions,
        LogRecordExportProcessorOptions processorOptions,
        Func<BaseExporter<LogRecord>, BaseExporter<LogRecord>>? configureExporterInstance = null)
    {
        if (sp == null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(processorOptions != null, "processorOptions was null");

        var config = sp.GetRequiredService<IConfiguration>();

        var sdkLimitOptions = new SdkLimitOptions(config);
        var experimentalOptions = new ExperimentalOptions(config);

        BaseExporter<LogRecord> otlpExporter = new OtlpLogExporter(
            exporterOptions!,
            sdkLimitOptions,
            experimentalOptions);

        if (configureExporterInstance != null)
        {
            otlpExporter = configureExporterInstance(otlpExporter);
        }

        if (processorOptions!.ExportProcessorType == ExportProcessorType.Simple)
        {
            return new SimpleLogRecordExportProcessor(otlpExporter);
        }
        else
        {
            var batchOptions = processorOptions.BatchExportProcessorOptions;

            return new BatchLogRecordExportProcessor(
                otlpExporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        }
    }

    private static OtlpExporterOptions GetOtlpExporterOptions(IServiceProvider sp, string? name, string finalName)
    {
        // Note: If OtlpExporter has been registered for tracing and/or metrics
        // then IOptionsFactory will be set by a call to
        // OtlpExporterOptions.RegisterOtlpExporterOptionsFactory. However, if we
        // are only using logging, we don't have an opportunity to do that
        // registration so we manually create a factory.

        var optionsFactory = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>();
        if (optionsFactory is not DelegatingOptionsFactory<OtlpExporterOptions>)
        {
            optionsFactory = new DelegatingOptionsFactory<OtlpExporterOptions>(
                (c, n) => OtlpExporterOptions.CreateOtlpExporterOptions(sp, c, n),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<OtlpExporterOptions>>(),
                sp.GetServices<IPostConfigureOptions<OtlpExporterOptions>>(),
                sp.GetServices<IValidateOptions<OtlpExporterOptions>>());

            return optionsFactory.Create(finalName);
        }

        if (name == null)
        {
            // If we are NOT using named options we create a new
            // instance always. The reason for this is
            // OtlpExporterOptions is shared by all signals. Without a
            // name, delegates for all signals will mix together.
            return optionsFactory.Create(finalName);
        }

        // If we have a valid factory AND we are using named options, we can
        // safely use the Options API fully.
        return sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalName);
    }
}
