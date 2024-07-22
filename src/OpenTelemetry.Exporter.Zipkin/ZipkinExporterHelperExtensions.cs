// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

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

        return builder.ConfigureServices(services =>
        {
            if (configure != null)
            {
                services.Configure(name, configure);
            }

            services.RegisterOptionsFactory(
                (sp, configuration, name) => new ZipkinExporterOptions(
                    configuration,
                    sp.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name)));

            services
                .AddOptions<ZipkinExporterOptions>(name)
                .Configure<IOptionsMonitor<BatchExportActivityProcessorOptions>>(
                    (exporterOptions, batchOptionsMonitor) =>
                    {
                        var defaultBatchOptions = batchOptionsMonitor.Get(name);

                        var exporterBatchOptions = exporterOptions.BatchExportProcessorOptions;
                        if (exporterBatchOptions != null
                            && exporterBatchOptions != defaultBatchOptions)
                        {
                            // Note: By default
                            // ZipkinExporterOptions.BatchExportProcessorOptions is
                            // set to BatchExportActivityProcessorOptions retrieved
                            // from DI. But users may change it via public setter so
                            // this code makes sure any changes are reflected on the
                            // DI instance so the call to AddBatchExportProcessor
                            // picks them up.
                            exporterBatchOptions.ApplyTo(defaultBatchOptions);
                        }
                    });

            services.ConfigureOpenTelemetryTracerProvider(
                (sp, builder) => AddZipkinExporter(sp, builder, name));
        });
    }

    private static void AddZipkinExporter(
        IServiceProvider serviceProvider,
        TracerProviderBuilder builder,
        string name)
    {
        var exporterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ZipkinExporterOptions>>().Get(name);

        if (exporterOptions.HttpClientFactory == ZipkinExporterOptions.DefaultHttpClientFactory)
        {
            exporterOptions.HttpClientFactory = () =>
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

        var zipkinExporter = new ZipkinExporter(exporterOptions);

        if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            builder.AddSimpleExportProcessor(zipkinExporter);
        }
        else
        {
            builder.AddBatchExportProcessor(zipkinExporter);
        }
    }
}
