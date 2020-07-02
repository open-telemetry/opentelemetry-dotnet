// <copyright file="OpenTelemetryBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Exporter.Console
{
    public static class OpenTelemetryBuilderExtensions
    {
        /// <summary>
        /// Adds new processing pipeline and registers a ConsoleActivity exporter to it.
        /// </summary>
        /// <param name="builder">Open Telemetry builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder UseConsoleActivityExporter(this OpenTelemetryBuilder builder, Action<ConsoleActivityExporterOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var exporterOptions = new ConsoleActivityExporterOptions();
            configure(exporterOptions);
            var consoleExporter = new ConsoleActivityExporter(exporterOptions);
            return builder.AddProcessorPipeline(pipeline => pipeline.SetExporter(consoleExporter));
        }

        /// <summary>
        /// Registers a ConsoleActivity exporter to a processing pipeline.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <param name="processorConfigure">Activity processor configuration.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder UseConsoleActivityExporter(this OpenTelemetryBuilder builder, Action<ConsoleActivityExporterOptions> configure, Action<ActivityProcessorPipelineBuilder> processorConfigure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (processorConfigure == null)
            {
                throw new ArgumentNullException(nameof(processorConfigure));
            }

            return builder.AddProcessorPipeline(pipeline =>
            {
                var exporterOptions = new ConsoleActivityExporterOptions();
                configure(exporterOptions);

                var consoleExporter = new ConsoleActivityExporter(exporterOptions);
                pipeline.SetExporter(consoleExporter);
                processorConfigure(pipeline);
            });
        }
    }
}
