// <copyright file="TracerBuilderExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public static class TracerBuilderExtensions
    {
        /// <summary>
        /// Enables OpenTelemetry Protocol (OTLP) exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Configuration action.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseOpenTelemetryProtocolExporter(this TracerBuilder builder, Action<ExporterOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var configuration = new ExporterOptions();
            configure(configuration);
            return builder.AddProcessorPipeline(b => b
                .SetExporter(new SpanDataExporter(configuration))
                .SetExportingProcessor(e => new BatchingSpanProcessor(e)));
        }

        /// <summary>
        /// Enables OpenTelemetry Protocol (OTLP) exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Configuration action.</param>
        /// <param name="processorConfigure">Span processor configuration action.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseOpenTelemetryProtocolExporter(
            this TracerBuilder builder, Action<ExporterOptions> configure, Action<SpanProcessorPipelineBuilder> processorConfigure)
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

            var configuration = new ExporterOptions();
            configure(configuration);
            return builder.AddProcessorPipeline(b =>
            {
                b.SetExporter(new SpanDataExporter(configuration));
                processorConfigure.Invoke(b);
            });
        }
    }
}
