// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
}
