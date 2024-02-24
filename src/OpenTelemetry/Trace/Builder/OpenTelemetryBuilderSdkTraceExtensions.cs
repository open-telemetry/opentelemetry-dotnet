// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

public static class OpenTelemetryBuilderSdkTraceExtensions
{
    public static IOpenTelemetryBuilder ConfigureTracingExportProcessorOptions(
        this IOpenTelemetryBuilder builder,
        Action<ActivityExportProcessorOptions> configure)
        => ConfigureTracingExportProcessorOptions(builder, name: null, configure);

    public static IOpenTelemetryBuilder ConfigureTracingExportProcessorOptions(
        this IOpenTelemetryBuilder builder,
        string? name,
        Action<ActivityExportProcessorOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        builder.Services.Configure(name, configure);

        return builder;
    }
}
