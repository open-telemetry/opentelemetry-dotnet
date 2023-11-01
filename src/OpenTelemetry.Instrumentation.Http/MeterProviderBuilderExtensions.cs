// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
#endif

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of HttpClient instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
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

        builder.ConfigureServices(services =>
        {
            services.RegisterOptionsFactory(configuration => new HttpClientMetricInstrumentationOptions(configuration));
        });

#if NETFRAMEWORK
        builder.AddMeter(HttpWebRequestActivitySource.MeterName);

        if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            deferredMeterProviderBuilder.Configure((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<HttpClientMetricInstrumentationOptions>>().Get(Options.DefaultName);

                HttpWebRequestActivitySource.MetricsOptions = options;
            });
        }
#else
        builder.AddMeter(HttpHandlerMetricsDiagnosticListener.MeterName);

        builder.AddInstrumentation(sp => new HttpClientMetrics(
            sp.GetRequiredService<IOptionsMonitor<HttpClientMetricInstrumentationOptions>>().Get(Options.DefaultName)));
#endif
        return builder;
#endif
    }
}
