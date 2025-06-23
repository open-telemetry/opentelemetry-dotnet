// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Trace;

internal static class MyExporterExtensions
{
    public static TracerProviderBuilder AddMyExporter(this TracerProviderBuilder builder)
    {
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
#endif

#pragma warning disable CA2000 // Dispose objects before losing scope
        return builder.AddProcessor(new BatchActivityExportProcessor(new MyExporter()));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
