// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET8_0_OR_GREATER
#if !NETFRAMEWORK
using OpenTelemetry.Instrumentation.Http;
#endif
using OpenTelemetry.Instrumentation.Http.Implementation;
#endif

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of HttpClient instrumentation.
/// </summary>
public static class HttpClientMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables HttpClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddHttpClientInstrumentation(
        this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

#if NET8_0_OR_GREATER
        return builder
            .AddMeter("System.Net.Http")
            .AddMeter("System.Net.NameResolution");
#else
        // Note: Warm-up the status code and method mapping.
        _ = TelemetryHelper.BoxedStatusCodes;
        _ = RequestMethodHelper.KnownMethods;

#if NETFRAMEWORK
        builder.AddMeter(HttpWebRequestActivitySource.MeterName);
#else
        builder.AddMeter(HttpHandlerMetricsDiagnosticListener.MeterName);

        builder.AddInstrumentation(new HttpClientMetrics());
#endif
        return builder;
#endif
    }
}
