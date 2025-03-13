// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics;
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
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="ZipkinExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddZipkinExporter(
        this TracerProviderBuilder builder,
        string? name,
        Action<ZipkinExporterOptions>? configure)
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

            return BuildZipkinExporterProcessor(options, sp);
        });
    }

    private static BaseProcessor<Activity> BuildZipkinExporterProcessor(
        ZipkinExporterOptions options,
        IServiceProvider serviceProvider)
    {
        if (options.HttpClientFactory == ZipkinExporterOptions.DefaultHttpClientFactory)
        {
            options.HttpClientFactory = () =>
            {
                Type? httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
                if (httpClientFactoryType != null)
                {
                    object? httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
                    if (httpClientFactory != null)
                    {
                        MethodInfo? createClientMethod = httpClientFactoryType.GetMethod(
                            "CreateClient",
                            BindingFlags.Public | BindingFlags.Instance,
                            binder: null,
                            [typeof(string)],
                            modifiers: null);
                        if (createClientMethod != null)
                        {
                            var parameters = new object[] { "ZipkinExporter" };
                            var client = (HttpClient?)createClientMethod.Invoke(httpClientFactory, parameters);
                            return client ?? new HttpClient();
                        }
                    }
                }

                return new HttpClient();
            };
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        var zipkinExporter = new ZipkinExporter(options);
#pragma warning restore CA2000 // Dispose objects before losing scope

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
