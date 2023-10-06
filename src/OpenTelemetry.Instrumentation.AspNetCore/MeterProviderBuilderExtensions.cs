// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of ASP.NET Core request instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetCoreInstrumentation(
        this MeterProviderBuilder builder)
        => AddAspNetCoreInstrumentation(builder, name: null, configureAspNetCoreInstrumentationOptions: null);

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configureAspNetCoreInstrumentationOptions">Callback action for configuring <see cref="AspNetCoreMetricsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetCoreInstrumentation(
        this MeterProviderBuilder builder,
        Action<AspNetCoreMetricsInstrumentationOptions> configureAspNetCoreInstrumentationOptions)
        => AddAspNetCoreInstrumentation(builder, name: null, configureAspNetCoreInstrumentationOptions);

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureAspNetCoreInstrumentationOptions">Callback action for configuring <see cref="AspNetCoreMetricsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetCoreInstrumentation(
        this MeterProviderBuilder builder,
        string name,
        Action<AspNetCoreMetricsInstrumentationOptions> configureAspNetCoreInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        // Note: Warm-up the status code mapping.
        _ = TelemetryHelper.BoxedStatusCodes;

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (configureAspNetCoreInstrumentationOptions != null)
            {
                services.Configure(name, configureAspNetCoreInstrumentationOptions);
            }

            services.RegisterOptionsFactory(configuration => new AspNetCoreMetricsInstrumentationOptions(configuration));
        });

        builder.AddMeter(AspNetCoreMetrics.InstrumentationName);

        builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<AspNetCoreMetricsInstrumentationOptions>>().Get(name);

            // TODO: Add additional options to AspNetCoreMetricsInstrumentationOptions ?
            //   RecordException - probably doesn't make sense for metric instrumentation
            //   EnableGrpcAspNetCoreSupport - this instrumentation will also need to also handle gRPC requests

            return new AspNetCoreMetrics(options);
        });

        return builder;
    }
}
