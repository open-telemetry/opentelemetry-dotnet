// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddOtlpExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
        => AddOtlpExporter(loggerOptions, name: null, configure: null);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddOtlpExporter instead this method will be removed in a future version.")]
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
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddOtlpExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        string? name,
        Action<OtlpExporterOptions>? configure)
    {
        Guard.ThrowIfNull(loggerOptions);

        var finalOptionsName = name ?? Options.DefaultName;

        return loggerOptions.AddProcessor(sp =>
        {
            var exporterOptions = GetOptions(sp, name, finalOptionsName, OtlpExporterOptions.CreateOtlpExporterOptions);

            var processorOptions = sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName);

            configure?.Invoke(exporterOptions);

            return BuildOtlpLogExporter(
                sp,
                exporterOptions,
                processorOptions,
                GetOptions(sp, Options.DefaultName, Options.DefaultName, (sp, c, n) => new SdkLimitOptions(c)),
                GetOptions(sp, name, finalOptionsName, (sp, c, n) => new ExperimentalOptions(c)));
        });
    }

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configureExporterAndProcessor">Callback action for configuring <see cref="OtlpExporterOptions"/> and <see cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddOtlpExporter instead this method will be removed in a future version.")]
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
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddOtlpExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        string? name,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions>? configureExporterAndProcessor)
    {
        Guard.ThrowIfNull(loggerOptions);

        var finalOptionsName = name ?? Options.DefaultName;

        return loggerOptions.AddProcessor(sp =>
        {
            var exporterOptions = GetOptions(sp, name, finalOptionsName, OtlpExporterOptions.CreateOtlpExporterOptions);

            var processorOptions = sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName);

            configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);

            return BuildOtlpLogExporter(
                sp,
                exporterOptions,
                processorOptions,
                GetOptions(sp, Options.DefaultName, Options.DefaultName, (sp, c, n) => new SdkLimitOptions(c)),
                GetOptions(sp, name, finalOptionsName, (sp, c, n) => new ExperimentalOptions(c)));
        });
    }

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
        => AddOtlpExporter(builder, name: null, configureExporter);

    /// <summary>
    /// Adds an OTLP exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporterAndProcessor">Callback action for
    /// configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder, Action<OtlpExporterOptions, LogRecordExportProcessorOptions> configureExporterAndProcessor)
        => AddOtlpExporter(builder, name: null, configureExporterAndProcessor);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporter">Optional callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
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

            services.AddOtlpExporterLoggingServices();
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
            var sdkLimitOptions = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildOtlpLogExporter(
                sp,
                exporterOptions,
                sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName),
                sdkLimitOptions,
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName));
        });
    }

    /// <summary>
    /// Adds an OTLP exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporterAndProcessor">Optional callback action for
    /// configuring <see cref="OtlpExporterOptions"/> and <see
    /// cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddOtlpExporter(
        this LoggerProviderBuilder builder,
        string? name,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions>? configureExporterAndProcessor)
    {
        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services => services.AddOtlpExporterLoggingServices());

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
            }
            else
            {
                // When using named options we can properly utilize Options
                // API to create or reuse an instance.
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
            }

            var processorOptions = sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(finalOptionsName);

            // Configuration delegate is executed inline.
            configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);

            // Note: Not using finalOptionsName here for SdkLimitOptions.
            // There should only be one provider for a given service
            // collection so SdkLimitOptions is treated as a single default
            // instance.
            var sdkLimitOptions = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildOtlpLogExporter(
                sp,
                exporterOptions,
                processorOptions,
                sdkLimitOptions,
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName));
        });
    }

    internal static BaseProcessor<LogRecord> BuildOtlpLogExporter(
        IServiceProvider serviceProvider,
        OtlpExporterOptions exporterOptions,
        LogRecordExportProcessorOptions processorOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        bool skipUseOtlpExporterRegistrationCheck = false,
        Func<BaseExporter<LogRecord>, BaseExporter<LogRecord>>? configureExporterInstance = null)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(processorOptions != null, "processorOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
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

        BaseExporter<LogRecord> otlpExporter = new OtlpLogExporter(
            exporterOptions!,
            sdkLimitOptions!,
            experimentalOptions!);

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

    private static T GetOptions<T>(
        IServiceProvider sp,
        string? name,
        string finalName,
        Func<IServiceProvider, IConfiguration, string, T> createOptionsFunc)
        where T : class, new()
    {
        // Note: If OtlpExporter has been registered for tracing and/or metrics
        // then IOptionsFactory will be set by a call to
        // OtlpExporterOptions.RegisterOtlpExporterOptionsFactory. However, if we
        // are only using logging, we don't have an opportunity to do that
        // registration so we manually create a factory.

        var optionsFactory = sp.GetRequiredService<IOptionsFactory<T>>();
        if (optionsFactory is not DelegatingOptionsFactory<T>)
        {
            optionsFactory = new DelegatingOptionsFactory<T>(
                (c, n) => createOptionsFunc(sp, c, n),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<T>>(),
                sp.GetServices<IPostConfigureOptions<T>>(),
                sp.GetServices<IValidateOptions<T>>());

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
        return sp.GetRequiredService<IOptionsMonitor<T>>().Get(finalName);
    }
}
