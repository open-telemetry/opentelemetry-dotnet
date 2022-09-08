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
        /// Adds Console exporter with OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddConsoleExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<ConsoleExporterOptions> configure = null)
        {
            Guard.ThrowIfNull(loggerOptions);

            if (configure != null)
            {
                loggerOptions.ConfigureServices(services => services.Configure(configure));
            }

            return loggerOptions.ConfigureProvider((sp, provider) =>
            {
                var options = sp.GetRequiredService<IOptions<ConsoleExporterOptions>>().Value;

                provider.AddProcessor(new SimpleLogRecordExportProcessor(new ConsoleLogRecordExporter(options)));
            });
        }
    }
}
