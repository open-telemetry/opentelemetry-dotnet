// <copyright file="OtlpLogExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using System.Diagnostics;

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
        => AddOtlpExporterInternal(loggerOptions, configure: null);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions> configure)
        => AddOtlpExporterInternal(loggerOptions, configure);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder)
           => AddOtlpExporter(builder, name: null, configureExporter: null);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder, Action<OtlpExporterOptions> configureExporter)
     => AddOtlpExporter(builder, name: null, configureExporter: configureExporter);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporterAndProcessor">Callback action for
    /// configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder, Action<OtlpExporterOptions, LogRecordExportProcessorOptions> configureExporterAndProcessor)
     => AddOtlpExporter(builder, name: null, configureExporterAndProcessor: configureExporterAndProcessor);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(
        this LoggerProviderBuilder builder,
        string name,
        Action<OtlpExporterOptions> configureExporter)
    {
        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (name != null && configureExporter != null)
            {
                // If we are using named options we register the
                // configuration delegate into options pipeline.
                services.Configure(finalOptionsName, configureExporter);
            }

            OtlpExporterOptions.RegisterOtlpExporterOptionsFactory(services);
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
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
                configureExporter?.Invoke(exporterOptions);
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
            var sdkOptionsManager = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildOtlpLogExporterProcessor(
                exporterOptions,
                sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName),
                sdkOptionsManager,
                sp);
        });
    }

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureExporterAndProcessor">Callback action for
    /// configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(
        this LoggerProviderBuilder builder,
        string name,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions> configureExporterAndProcessor)
    {
        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            OtlpExporterOptions.RegisterOtlpExporterOptionsFactory(services);
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        });

        return builder.AddProcessor(sp =>
        {
            OtlpExporterOptions exporterOptions;
            LogRecordExportProcessorOptions processorOptions;

            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // OtlpExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together. See:
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/4043
                exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>().Create(finalOptionsName);
                processorOptions = sp.GetRequiredService<IOptionsFactory<LogRecordExportProcessorOptions>>().Create(finalOptionsName);

                // Configuration delegate is executed inline on the fresh instance.
                configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);
            }
            else
            {
                // When using named options we can properly utilize Options
                // API to create or reuse an instance.
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
                processorOptions = sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName);
            }

            // Note: Not using finalOptionsName here for SdkLimitOptions.
            // There should only be one provider for a given service
            // collection so SdkLimitOptions is treated as a single default
            // instance.
            var sdkOptionsManager = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildOtlpLogExporterProcessor(
                exporterOptions,
                processorOptions,
                sdkOptionsManager,
                sp);
        });
    }

    private static BaseProcessor<LogRecord> BuildOtlpLogExporterProcessor(OtlpExporterOptions exporterOptions, LogRecordExportProcessorOptions processorOptions, SdkLimitOptions sdkLimitOptions, IServiceProvider sp)
    {
        /*
         * Note:
         *
         * We don't currently enable IHttpClientFactory for OtlpLogExporter.
         *
         * The DefaultHttpClientFactory requires the ILoggerFactory in its ctor:
         * https://github.com/dotnet/runtime/blob/fa40ecf7d36bf4e31d7ae968807c1c529bac66d6/src/libraries/Microsoft.Extensions.Http/src/DefaultHttpClientFactory.cs#L64
         *
         * This creates a circular reference: ILoggerFactory ->
         * OpenTelemetryLoggerProvider -> OtlpLogExporter -> IHttpClientFactory
         * -> ILoggerFactory
         *
         * exporterOptions.TryEnableIHttpClientFactoryIntegration(sp,
         * "OtlpLogExporter");
         */

        BaseExporter<LogRecord> otlpExporter = new OtlpLogExporter(exporterOptions, sdkLimitOptions);

        if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            return new SimpleLogRecordExportProcessor(otlpExporter);
        }
        else
        {
            return new BatchLogRecordExportProcessor(
                otlpExporter,
                processorOptions.BatchExportProcessorOptions.MaxQueueSize,
                processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                processorOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                processorOptions.BatchExportProcessorOptions.MaxExportBatchSize);
        }
    }

    private static OpenTelemetryLoggerOptions AddOtlpExporterInternal(
        OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions> configure)
    {
        var exporterOptions = new OtlpExporterOptions();
        var processorOptions = new LogRecordExportProcessorOptions();

        configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);

        return loggerOptions.AddProcessor(BuildOtlpLogExporter(exporterOptions, processorOptions));
    }

    internal static BaseProcessor<LogRecord> BuildOtlpLogExporter(
        OtlpExporterOptions exporterOptions,
        LogRecordExportProcessorOptions processorOptions,
        Func<BaseExporter<LogRecord>, BaseExporter<LogRecord>> configureExporterInstance = null)
    {
        BaseExporter<LogRecord> otlpExporter = new OtlpLogExporter(exporterOptions);

        if (configureExporterInstance != null)
        {
            otlpExporter = configureExporterInstance(otlpExporter);
        }

        if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
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

    private static OpenTelemetryLoggerOptions AddOtlpExporterInternal(
    OpenTelemetryLoggerOptions loggerOptions,
    Action<OtlpExporterOptions> configure)
    {
        var exporterOptions = new OtlpExporterOptions();

        configure?.Invoke(exporterOptions);

        return loggerOptions.AddProcessor(BuildOtlpLogExporter(exporterOptions, new()));
    }
}
