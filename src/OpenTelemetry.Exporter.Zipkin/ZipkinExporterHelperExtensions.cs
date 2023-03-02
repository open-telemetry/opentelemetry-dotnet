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

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddZipkinExporter(this TracerProviderBuilder builder)
            => AddZipkinExporter(builder, name: null, configure: null);

        /// <summary>
        /// Adds Zipkin exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="ZipkinExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddZipkinExporter(this TracerProviderBuilder builder, Action<ZipkinExporterOptions> configure)
            => AddZipkinExporter(builder, name: null, configure);

        /// <summary>
        /// Adds Zipkin exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="ZipkinExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddZipkinExporter(
            this TracerProviderBuilder builder,
            string name,
            Action<ZipkinExporterOptions> configure)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            builder.ConfigureServices(services =>
            {
                if (configure != null)
                {
                    services.Configure(name, configure);
                }

                services.RegisterOptionsFactory(
                    (sp, configuration, name) => new ZipkinExporterOptions(
                        configuration,
                        sp.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name)));
            });

            return builder.AddProcessor(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<ZipkinExporterOptions>>().Get(name);

                return BuildZipkinExporterProcessor(builder, options, sp);
            });
        }

        private static BaseProcessor<Activity> BuildZipkinExporterProcessor(
            TracerProviderBuilder builder,
            ZipkinExporterOptions options,
            IServiceProvider serviceProvider)
        {
            if (options.HttpClientFactory == ZipkinExporterOptions.DefaultHttpClientFactory)
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
                return new SimpleActivityExportProcessor(zipkinExporter);
            }
            else
            {
                return new BatchActivityExportProcessor(
                    zipkinExporter,
                    options.BatchExportProcessorOptions.MaxQueueSize,
                    options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    options.BatchExportProcessorOptions.MaxExportBatchSize);
            }
        }
    }
}
