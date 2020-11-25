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

#if NET461 || NETSTANDARD2_0
using System;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Console;

namespace OpenTelemetry.Logs
{
    public static class ConsoleExporterLoggingExtensions
    {
        /// <summary>
        /// Adds Console Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<ConsoleExporterOptions> configure = null)
        {
            if (loggerOptions == null)
            {
                throw new ArgumentNullException(nameof(loggerOptions));
            }

            var options = new ConsoleExporterOptions();
            configure?.Invoke(options);
            return loggerOptions.AddProcessor(new SimpleExportProcessor<LogRecord>(new ConsoleLogRecordExporter(options)));
        }
    }
}
#endif
