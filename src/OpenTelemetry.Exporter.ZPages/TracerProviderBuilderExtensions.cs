// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter.ZPages;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of Zipkin exporter.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Registers a Zipkin exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <param name="processorConfigure">Activity processor configuration.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder UseZPagesExporter(
            this TracerProviderBuilder builder,
            Action<ZPagesExporterOptions> configure = null,
            Action<ActivityProcessorPipelineBuilder> processorConfigure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddProcessorPipeline(pipeline =>
            {
                var options = new ZPagesExporterOptions();
                configure?.Invoke(options);

                var exporter = new ZPagesExporter(options);
                processorConfigure?.Invoke(pipeline);
                pipeline.SetExporter(exporter);
            });
        }
    }
}
