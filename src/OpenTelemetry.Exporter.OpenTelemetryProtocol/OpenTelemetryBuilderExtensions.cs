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
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public static class OpenTelemetryBuilderExtensions
    {
        /// <summary>
        /// Enables the OpenTelemetry Protocol (OTLP) exporter.
        /// </summary>
        /// <param name="builder">Open Telemetry builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <param name="processorConfigure">Activity processor configuration.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder UseOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions> configure = null, Action<ActivityProcessorPipelineBuilder> processorConfigure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddProcessorPipeline(pipeline =>
            {
                var exporterOptions = new OtlpExporterOptions();
                configure?.Invoke(exporterOptions);

                var activityExporter = new OtlpExporter(exporterOptions);
                processorConfigure?.Invoke(pipeline);
                pipeline.SetExporter(activityExporter);
            });
        }
    }
}
