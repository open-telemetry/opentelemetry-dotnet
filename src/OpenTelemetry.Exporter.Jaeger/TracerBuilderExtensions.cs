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
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Extension methods to simplify registering a Jaeger exporter.
    /// </summary>
    public static class TracerBuilderExtensions
    {
        /// <summary>
        /// Registers a Jaeger exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseJaeger(this TracerBuilder builder, Action<JaegerExporterOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new JaegerExporterOptions();
            configure(options);
            return builder.AddProcessorPipeline(b => b
                .SetExporter(new JaegerTraceExporter(options))
                .SetExportingProcessor(e => new BatchingSpanProcessor(e)));
        }

        /// <summary>
        /// Registers Jaeger exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <param name="processorConfigure">Span processor configuration.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseJaeger(this TracerBuilder builder, Action<JaegerExporterOptions> configure, Action<SpanProcessorPipelineBuilder> processorConfigure)
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

            var options = new JaegerExporterOptions();
            configure(options);
            return builder.AddProcessorPipeline(b =>
            {
                b.SetExporter(new JaegerTraceExporter(options));
                processorConfigure.Invoke(b);
            });
        }

        /// <summary>
        /// Registers a Jaeger exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder UseJaegerActivityExporter(this OpenTelemetryBuilder builder, Action<JaegerExporterOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            return builder.AddProcessorPipeline(pipeline =>
            {
                var exporterOptions = new JaegerExporterOptions();
                configure(exporterOptions);

                var activityExporter = new JaegerActivityExporter(exporterOptions);
                pipeline.SetExporter(activityExporter);
            });
        }
    }
}
