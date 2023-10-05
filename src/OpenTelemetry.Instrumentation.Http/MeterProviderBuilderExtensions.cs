// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
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

        // Note: Warm-up the status code mapping.
        _ = TelemetryHelper.BoxedStatusCodes;

        builder.ConfigureServices(services =>
        {
            services.RegisterOptionsFactory(configuration => new HttpClientMetricInstrumentationOptions(configuration));
        });

        // TODO: Handle HttpClientMetricInstrumentationOptions
        //   SetHttpFlavor - seems like this would be handled by views
        //   Filter - makes sense for metric instrumentation
        //   Enrich - do we want a similar kind of functionality for metrics?
        //   RecordException - probably doesn't make sense for metric instrumentation

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
    }
}
