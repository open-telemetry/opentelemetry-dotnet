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

using System.Text.Json;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    public static class ConsoleExporterLoggingExtensions
    {
        /// <summary>
        /// Adds Console exporter with OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddConsoleExporter(loggerOptions, configure: null);

        /// <summary>
        /// Adds Console exporter with OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<ConsoleExporterOptions> configure)
        {
            Guard.ThrowIfNull(loggerOptions);

            var options = new ConsoleExporterOptions();
            configure?.Invoke(options);
            return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options)));
        }

        /// <summary>
        /// Adds a JSON Console exporter with default OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddConsoleJsonExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddConsoleJsonExporter(loggerOptions, configure: null, jsonSerializerOptionsConfigure: null);

        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
        /// <param name="jsonSerializerOptionsConfigure">Callback action for configuring JSON Serializer Options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddConsoleJsonExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<ConsoleExporterOptions> configure, Action<JsonSerializerOptions> jsonSerializerOptionsConfigure)
        {
            Guard.ThrowIfNull(loggerOptions);

            var options = new ConsoleExporterOptions();
            configure?.Invoke(options);
            var jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptionsConfigure?.Invoke(jsonSerializerOptions);
            return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordJsonExporter(options, jsonSerializerOptions)));
        }
    }
}
