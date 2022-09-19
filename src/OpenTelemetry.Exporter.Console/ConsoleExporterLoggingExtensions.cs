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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    public static class ConsoleExporterLoggingExtensions
    {
        /// <summary>
        /// Adds Console exporter to the OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("Call the AddConsoleExporter extension using LoggerProviderBuilder instead this method will be removed in a future version.")]
        public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddConsoleExporter(loggerOptions, configure: null);

        /// <summary>
        /// Adds Console exporter to the OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("Call the AddConsoleExporter extension using LoggerProviderBuilder instead this method will be removed in a future version.")]
        public static OpenTelemetryLoggerOptions AddConsoleExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<ConsoleExporterOptions> configure)
        {
            Guard.ThrowIfNull(loggerOptions);

            var options = new ConsoleExporterOptions();
            configure?.Invoke(options);
            return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options)));
        }

        /// <summary>
        /// Adds Console exporter to the LoggerProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="LoggerProviderBuilder"/>.</param>
        /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
        public static LoggerProviderBuilder AddConsoleExporter(this LoggerProviderBuilder builder)
            => AddConsoleExporter(builder, name: null, configure: null);

        /// <summary>
        /// Adds Console exporter to the LoggerProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="LoggerProviderBuilder"/>.</param>
        /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
        public static LoggerProviderBuilder AddConsoleExporter(
            this LoggerProviderBuilder builder,
            Action<ConsoleExporterOptions> configure)
            => AddConsoleExporter(builder, name: null, configure);

        /// <summary>
        /// Adds Console exporter to the LoggerProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="LoggerProviderBuilder"/>.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
        public static LoggerProviderBuilder AddConsoleExporter(
            this LoggerProviderBuilder builder,
            string name,
            Action<ConsoleExporterOptions> configure)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            if (configure != null)
            {
                builder.ConfigureServices(services => services.Configure(name, configure));
            }

            builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);

                builder.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options)));
            });

            return builder;
        }
    }
}
