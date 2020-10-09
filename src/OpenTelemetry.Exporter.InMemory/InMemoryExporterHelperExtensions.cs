// <copyright file="InMemoryExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Exporter;
#if NETSTANDARD2_0
using OpenTelemetry.Logs;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry
{
    public static class InMemoryExporterHelperExtensions
    {
        /// <summary>
        /// Adds InMemory exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public static TracerProviderBuilder AddInMemoryExporter(this TracerProviderBuilder builder, Action<InMemoryExporterOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new InMemoryExporterOptions();
            configure?.Invoke(options);
            return builder.AddProcessor(new SimpleExportProcessor<Activity>(new InMemoryExporter<Activity>(options)));
        }

#if NETSTANDARD2_0
        public static OpenTelemetryLoggerOptions AddInMemoryExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<InMemoryExporterOptions> configure = null)
        {
            if (loggerOptions == null)
            {
                throw new ArgumentNullException(nameof(loggerOptions));
            }

            var options = new InMemoryExporterOptions();
            configure?.Invoke(options);

            return loggerOptions.AddProcessor(new SimpleExportProcessor<LogRecord>(new InMemoryExporter<LogRecord>(options)));
        }
#endif
    }
}
