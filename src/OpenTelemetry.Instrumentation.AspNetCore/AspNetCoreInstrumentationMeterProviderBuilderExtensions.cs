// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET8_0_OR_GREATER
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of ASP.NET Core request instrumentation.
/// </summary>
public static class AspNetCoreInstrumentationMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetCoreInstrumentation(
        this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

#if NET8_0_OR_GREATER
        return builder.ConfigureMeters();
#else
        // Note: Warm-up the status code and method mapping.
        _ = TelemetryHelper.BoxedStatusCodes;
        _ = RequestMethodHelper.KnownMethods;

        builder.AddMeter(HttpInMetricsListener.InstrumentationName);

        builder.AddInstrumentation(new AspNetCoreMetrics());

        return builder;
#endif
    }

    internal static MeterProviderBuilder ConfigureMeters(this MeterProviderBuilder builder)
    {
        return builder
             .AddMeter("Microsoft.AspNetCore.Hosting")
             .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
             .AddMeter("Microsoft.AspNetCore.Http.Connections")
             .AddMeter("Microsoft.AspNetCore.Routing")
             .AddMeter("Microsoft.AspNetCore.Diagnostics")
             .AddMeter("Microsoft.AspNetCore.RateLimiting");
    }
}
