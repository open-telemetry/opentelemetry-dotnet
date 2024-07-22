// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

internal static class MyExporterExtensions
{
    public static LoggerProviderBuilder AddMyExporter(
        this LoggerProviderBuilder builder)
        => AddMyExporter(builder, name: null);

    public static LoggerProviderBuilder AddMyExporter(
        this LoggerProviderBuilder builder,
        string? name)
    {
        return builder.AddBatchExportProcessor(
            name,
            new MyExporter());
    }

    public static OpenTelemetryLoggerOptions AddMyExporter(
        this OpenTelemetryLoggerOptions options)
        => AddMyExporter(options, name: null);

    public static OpenTelemetryLoggerOptions AddMyExporter(
        this OpenTelemetryLoggerOptions options,
        string? name)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        ((IDeferredLoggerProviderBuilder)options).Configure(
            (sp, builder) => builder.AddBatchExportProcessor(name, new MyExporter()));

        return options;
    }
}
