// <copyright file="TracerBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Exporter.LightStep;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Extension methods to simplify registering of Zipkin exporter.
    /// </summary>
    public static class TracerBuilderExtensions
    {
        /// <summary>
        /// Registers LightStep exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Configuration options.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseLightStep(this TracerBuilder builder, Action<LightStepTraceExporterOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new LightStepTraceExporterOptions();
            configure(options);
            return builder.AddProcessorPipeline(b => b
                .SetExporter(new LightStepTraceExporter(options))
                .SetExportingProcessor(e => new BatchingSpanProcessor(e)));
        }

        /// <summary>
        /// Registers LightStep exporter.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Configuration options.</param>
        /// <param name="processorConfigure">Span processor configuration.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder UseLightStep(this TracerBuilder builder, Action<LightStepTraceExporterOptions> configure, Action<SpanProcessorPipelineBuilder> processorConfigure)
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

            var options = new LightStepTraceExporterOptions();
            configure(options);
            return builder.AddProcessorPipeline(b =>
            {
                b.SetExporter(new LightStepTraceExporter(options));
                processorConfigure.Invoke(b);
            });
        }
    }
}
