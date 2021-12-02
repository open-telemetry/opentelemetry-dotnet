// <copyright file="JaegerExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering a Jaeger exporter.
    /// </summary>
    public static class JaegerExporterHelperExtensions
    {
        /// <summary>
        /// Adds Jaeger exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="optionsBuilder"><see cref="JaegerExporterOptionsBuilder"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddJaegerExporter(
            this TracerProviderBuilder builder,
            JaegerExporterOptionsBuilder optionsBuilder)
        {
            Guard.Null(builder, nameof(builder));
            Guard.Null(optionsBuilder, nameof(optionsBuilder));

            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    AddJaegerExporter(builder, optionsBuilder, sp);
                });
            }

            return AddJaegerExporter(builder, optionsBuilder, serviceProvider: null);
        }

        /// <summary>
        /// Adds Jaeger exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter options configuration callback.</param>
        /// <param name="optionsBuilder"><see cref="JaegerExporterOptionsBuilder"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddJaegerExporter(
            this TracerProviderBuilder builder,
            Action<JaegerExporterOptions> configure = null,
            JaegerExporterOptionsBuilder optionsBuilder = null)
        {
            Guard.Null(builder, nameof(builder));

            optionsBuilder ??= new();

            if (configure != null)
            {
                optionsBuilder.Configure(configure);
            }

            return AddJaegerExporter(builder, optionsBuilder);
        }

        private static TracerProviderBuilder AddJaegerExporter(
            TracerProviderBuilder builder,
            JaegerExporterOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider)
        {
            var options = optionsBuilder.Build(serviceProvider);

            var exporter = new JaegerExporter(options);

            return builder.AddProcessor(exporter, options);
        }
    }
}
