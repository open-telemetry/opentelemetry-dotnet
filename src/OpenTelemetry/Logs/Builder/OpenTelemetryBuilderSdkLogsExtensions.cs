// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

public static class OpenTelemetryBuilderSdkLogsExtensions
{
    public static IOpenTelemetryBuilder ConfigureLoggingExportProcessorOptions(
        this IOpenTelemetryBuilder builder,
        Action<LogRecordExportProcessorOptions> configure)
        => ConfigureLoggingExportProcessorOptions(builder, name: null, configure);

    public static IOpenTelemetryBuilder ConfigureLoggingExportProcessorOptions(
        this IOpenTelemetryBuilder builder,
        string? name,
        Action<LogRecordExportProcessorOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        builder.Services.Configure(name, configure);

        return builder;
    }
}
