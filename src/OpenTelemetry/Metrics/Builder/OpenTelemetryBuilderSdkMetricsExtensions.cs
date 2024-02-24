// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry;

public static class OpenTelemetryBuilderSdkMetricsExtensions
{
    public static IOpenTelemetryBuilder ConfigureMetricsReaderOptions(
        this IOpenTelemetryBuilder builder,
        Action<MetricReaderOptions> configure)
        => ConfigureMetricsReaderOptions(builder, name: null, configure);

    public static IOpenTelemetryBuilder ConfigureMetricsReaderOptions(
        this IOpenTelemetryBuilder builder,
        string? name,
        Action<MetricReaderOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        builder.Services.Configure(name, configure);

        return builder;
    }
}
