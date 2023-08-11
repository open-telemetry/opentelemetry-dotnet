// <copyright file="ConsoleExporterLoggingExtensions.cs" company="OpenTelemetry Authors">
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
    /// todo: [Obsolete("Call LoggerProviderBuilder.AddConsoleExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions)
        => AddConsoleExporter(loggerOptions, configure: null);

    /// <summary>
    /// Adds Console exporter with OpenTelemetryLoggerOptions.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    /// todo: [Obsolete("Call LoggerProviderBuilder.AddConsoleExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<ConsoleExporterOptions> configure)
    {
        Guard.ThrowIfNull(loggerOptions);

        var options = new ConsoleExporterOptions();
        configure?.Invoke(options);
        return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options)));
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public
#else
    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal
#endif
        static LoggerProviderBuilder AddConsoleExporter(
        this LoggerProviderBuilder loggerProviderBuilder)
        => AddConsoleExporter(loggerProviderBuilder, name: null, configure: null);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <remarks><inheritdoc cref="AddConsoleExporter(LoggerProviderBuilder)" path="/remarks"/></remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public
#else
    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal
#endif
        static LoggerProviderBuilder AddConsoleExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        Action<ConsoleExporterOptions> configure)
        => AddConsoleExporter(loggerProviderBuilder, name: null, configure);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <remarks><inheritdoc cref="AddConsoleExporter(LoggerProviderBuilder)" path="/remarks"/></remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public
#else
    /// <summary>
    /// Adds Console exporter with LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal
#endif
        static LoggerProviderBuilder AddConsoleExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        string name,
        Action<ConsoleExporterOptions> configure)
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
