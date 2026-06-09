// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

internal static class MyExporterExtensions
{
    public static TracerProviderBuilder AddMyExporter(this TracerProviderBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddProcessor(new BatchActivityExportProcessor(new MyExporter()));
    }

    public static TracerProviderBuilder AddMyExporter(
        this TracerProviderBuilder builder,
        Action<MyExporterOptions> configure)
    {
        return builder.AddMyExporter(Options.DefaultName, configure);
    }

    public static TracerProviderBuilder AddMyExporter(
        this TracerProviderBuilder builder,
        string name,
        Action<MyExporterOptions> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        builder.ConfigureServices(services => services.Configure(name, configure));

        return builder.AddProcessor(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<MyExporterOptions>>().Get(name);

            return new BatchActivityExportProcessor(new MyExporter(options.Name));
        });
    }
}
