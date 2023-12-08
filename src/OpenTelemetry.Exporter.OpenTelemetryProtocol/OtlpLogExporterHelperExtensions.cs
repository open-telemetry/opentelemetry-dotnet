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

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds an OTLP exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder)
    => AddOtlpExporter(builder, name: null, configureExporter: null);

    /// <summary>
    /// Adds an OTLP exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder, Action<OtlpExporterOptions> configureExporter)
     => AddOtlpExporter(builder, name: null, configureExporter: configureExporter);

    /// <summary>
    /// Adds an OTLP exporter to the LoggerProvider.
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
        string? name,
        Action<OtlpExporterOptions>? configureExporter)
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
                sdkOptionsManager);
        });
    }

    /// <summary>
    /// Adds an OTLP exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureExporterAndProcessor">Callback action for
    /// configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(
        this LoggerProviderBuilder builder,
        string? name,
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
                sdkOptionsManager);
        });
    }
#endif

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

    internal static BaseProcessor<LogRecord> BuildOtlpLogExporterProcessor(OtlpExporterOptions exporterOptions, LogRecordExportProcessorOptions processorOptions, SdkLimitOptions sdkLimitOptions)
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

        BaseExporter<LogRecord> otlpExporter = new OtlpLogExporter(exporterOptions, sdkLimitOptions, experimentalOptions: new());

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
