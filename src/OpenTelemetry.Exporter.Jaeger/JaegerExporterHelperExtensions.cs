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

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddJaegerExporter(this TracerProviderBuilder builder)
            => AddJaegerExporter(builder, name: null, configure: null);

        /// <summary>
        /// Adds Jaeger exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="JaegerExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddJaegerExporter(this TracerProviderBuilder builder, Action<JaegerExporterOptions> configure)
            => AddJaegerExporter(builder, name: null, configure);

        /// <summary>
        /// Adds Jaeger exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="JaegerExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddJaegerExporter(
            this TracerProviderBuilder builder,
            string name,
            Action<JaegerExporterOptions> configure)
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
                    (sp, configuration) => new JaegerExporterOptions(
                        configuration,
                        sp.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name)));
            });

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<JaegerExporterOptions>>().Get(name);

                AddJaegerExporter(builder, options, sp);
            });
        }

        private static TracerProviderBuilder AddJaegerExporter(
            TracerProviderBuilder builder,
            JaegerExporterOptions options,
            IServiceProvider serviceProvider)
        {
            if (options.Protocol == JaegerExportProtocol.HttpBinaryThrift
                && options.HttpClientFactory == JaegerExporterOptions.DefaultHttpClientFactory)
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
                                return (HttpClient)createClientMethod.Invoke(httpClientFactory, new object[] { "JaegerExporter" });
                            }
                        }
                    }

                    return new HttpClient();
                };
            }

            var jaegerExporter = new JaegerExporter(options);

            if (options.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(jaegerExporter));
            }
            else
            {
                return builder.AddProcessor(new BatchActivityExportProcessor(
                    jaegerExporter,
                    options.BatchExportProcessorOptions.MaxQueueSize,
                    options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    options.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }
    }
}
