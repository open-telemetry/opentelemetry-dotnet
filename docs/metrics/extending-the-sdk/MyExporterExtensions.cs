// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

internal static class MyExporterExtensions
{
    public static MeterProviderBuilder AddMyExporter(this MeterProviderBuilder builder, int exportIntervalMilliSeconds = Timeout.Infinite)
    {
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
#endif

        if (exportIntervalMilliSeconds == Timeout.Infinite)
        {
            // Export triggered manually only.
#pragma warning disable CA2000 // Dispose objects before losing scope
            return builder.AddReader(new BaseExportingMetricReader(new MyExporter()));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        else
        {
            // Export is triggered periodically.
#pragma warning disable CA2000 // Dispose objects before losing scope
            return builder.AddReader(new PeriodicExportingMetricReader(new MyExporter(), exportIntervalMilliSeconds));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
    }
}
