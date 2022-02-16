// <copyright file="ZipkinExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Reflection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of Zipkin exporter.
    /// </summary>
    public static class ZipkinExporterHelperExtensions
    {
        /// <summary>
        /// Adds Zipkin exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public static TracerProviderBuilder AddZipkinExporter(this TracerProviderBuilder builder, Action<ZipkinExporterOptions> configure = null)
        {
            Guard.ThrowIfNull(builder);

            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    AddZipkinExporter(builder, sp.GetOptions<ZipkinExporterOptions>(), configure, sp);
                });
            }

            return AddZipkinExporter(builder, new ZipkinExporterOptions(), configure, serviceProvider: null);
        }

        private static TracerProviderBuilder AddZipkinExporter(
            TracerProviderBuilder builder,
            ZipkinExporterOptions options,
            Action<ZipkinExporterOptions> configure,
            IServiceProvider serviceProvider)
        {
            configure?.Invoke(options);

            if (serviceProvider != null && options.HttpClientFactory == ZipkinExporterOptions.DefaultHttpClientFactory)
            {
                options.HttpClientFactory = () =>
                {
                    Type httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
                    if (httpClientFactoryType != null)
                    {
                        object httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
                        if (httpClientFactory != null)
                        {
                            MethodInfo createClientMethod = httpClientFactoryType.GetMethod(
                                "CreateClient",
                                BindingFlags.Public | BindingFlags.Instance,
                                binder: null,
                                new Type[] { typeof(string) },
                                modifiers: null);
                            if (createClientMethod != null)
                            {
                                return (HttpClient)createClientMethod.Invoke(httpClientFactory, new object[] { "ZipkinExporter" });
                            }
                        }
                    }

                    return new HttpClient();
                };
            }

            var zipkinExporter = new ZipkinExporter(options);

            if (options.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(zipkinExporter));
            }
            else
            {
                return builder.AddProcessor(new BatchActivityExportProcessor(
                    zipkinExporter,
                    options.BatchExportProcessorOptions.MaxQueueSize,
                    options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    options.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }
    }
}
