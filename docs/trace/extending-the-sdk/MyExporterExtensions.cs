// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

internal static class MyExporterExtensions
{
    public static TracerProviderBuilder AddMyExporter(
        this TracerProviderBuilder builder)
        => AddMyExporter(builder, name: null);

    public static TracerProviderBuilder AddMyExporter(
        this TracerProviderBuilder builder,
        string? name)
    {
        return builder.AddBatchExportProcessor(
            name,
            new MyExporter());
    }
}
