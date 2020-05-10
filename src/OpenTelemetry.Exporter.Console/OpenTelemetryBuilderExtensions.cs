﻿// <copyright file="OpenTelemetryBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Console
{
    public static class OpenTelemetryBuilderExtensions
    {
        /// <summary>
        /// Registers a Console exporter.
        /// </summary>
        /// <param name="builder">Open Telemetry builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder UseConsoleActivity(this OpenTelemetryBuilder builder, Action<ConsoleActivityExporterOptions> configure)
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
            return builder.SetProcessorPipeline(pipeline => pipeline.SetExporter(consoleExporter));
        }

        /// <summary>
        /// Registers Console exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <param name="processorConfigure">Span processor configuration.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseConsole(this TracerBuilder builder, Action<ConsoleExporterOptions> configure, Action<SpanProcessorPipelineBuilder> processorConfigure)
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

            var options = new ConsoleExporterOptions();
            configure(options);
            return builder.AddProcessorPipeline(b =>
            {
                b.SetExporter(new ConsoleExporter(options));
                processorConfigure.Invoke(b);
            });
        }
    }
}
