// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

internal static class MyExporterExtensions
{
    public static TracerProviderBuilder AddMyExporter(
        this TracerProviderBuilder builder,
        string? name = null,
        Action<MyExporterOptions>? configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        return builder.AddProcessor(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<MyExporterOptions>>().Get(name);

            return new BatchActivityExportProcessor(new MyExporter(options.Name));
        });
    }
}

internal sealed class MyExporterOptions
{
    public string Name { get; set; } = "MyExporter";
}
