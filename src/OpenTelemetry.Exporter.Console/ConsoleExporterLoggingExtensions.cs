// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

public static class ConsoleExporterLoggingExtensions
{
    /// <summary>
    /// Adds Console exporter with OpenTelemetryLoggerOptions.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddConsoleExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions)
        => AddConsoleExporter(loggerOptions, configure: null);

    /// <summary>
    /// Adds Console exporter with OpenTelemetryLoggerOptions.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddConsoleExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<ConsoleExporterOptions>? configure)
    {
        Guard.ThrowIfNull(loggerOptions);

        var options = new ConsoleExporterOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000 // Dispose objects before losing scope
        return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options)));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddConsoleExporter(
        this LoggerProviderBuilder loggerProviderBuilder)
        => AddConsoleExporter(loggerProviderBuilder, name: null, configure: null);

    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddConsoleExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        Action<ConsoleExporterOptions> configure)
        => AddConsoleExporter(loggerProviderBuilder, name: null, configure);

    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddConsoleExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        string? name,
        Action<ConsoleExporterOptions>? configure)
    {
        Guard.ThrowIfNull(loggerProviderBuilder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            loggerProviderBuilder.ConfigureServices(services => services.Configure(name, configure));
        }

        return loggerProviderBuilder.AddProcessor(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);

            return new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options));
        });
    }
}
