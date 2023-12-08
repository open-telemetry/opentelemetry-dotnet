// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET7_0_OR_GREATER
using System.Diagnostics;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of ASP.NET Core request instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAspNetCoreInstrumentation(this TracerProviderBuilder builder)
        => AddAspNetCoreInstrumentation(builder, name: null, configureAspNetCoreTraceInstrumentationOptions: null);

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configureAspNetCoreTraceInstrumentationOptions">Callback action for configuring <see cref="AspNetCoreTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAspNetCoreInstrumentation(
        this TracerProviderBuilder builder,
        Action<AspNetCoreTraceInstrumentationOptions> configureAspNetCoreTraceInstrumentationOptions)
        => AddAspNetCoreInstrumentation(builder, name: null, configureAspNetCoreTraceInstrumentationOptions);

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureAspNetCoreTraceInstrumentationOptions">Callback action for configuring <see cref="AspNetCoreTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAspNetCoreInstrumentation(
        this TracerProviderBuilder builder,
        string name,
        Action<AspNetCoreTraceInstrumentationOptions> configureAspNetCoreTraceInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        // Note: Warm-up the status code and method mapping.
        _ = TelemetryHelper.BoxedStatusCodes;
        _ = RequestMethodHelper.KnownMethods;

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (configureAspNetCoreTraceInstrumentationOptions != null)
            {
                services.Configure(name, configureAspNetCoreTraceInstrumentationOptions);
            }

            services.RegisterOptionsFactory(configuration => new AspNetCoreTraceInstrumentationOptions(configuration));
        });

        if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            deferredTracerProviderBuilder.Configure((sp, builder) =>
            {
                AddAspNetCoreInstrumentationSources(builder, sp);
            });
        }

        return builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<AspNetCoreTraceInstrumentationOptions>>().Get(name);

            return new AspNetCoreInstrumentation(
                new HttpInListener(options));
        });
    }

    // Note: This is used by unit tests.
    internal static TracerProviderBuilder AddAspNetCoreInstrumentation(
        this TracerProviderBuilder builder,
        HttpInListener listener)
    {
        builder.AddAspNetCoreInstrumentationSources();

        return builder.AddInstrumentation(
            new AspNetCoreInstrumentation(listener));
    }

    private static void AddAspNetCoreInstrumentationSources(
        this TracerProviderBuilder builder,
        IServiceProvider serviceProvider = null)
    {
        // For .NET7.0 onwards activity will be created using activitySource.
        // https://github.com/dotnet/aspnetcore/blob/bf3352f2422bf16fa3ca49021f0e31961ce525eb/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L327
        // For .NET6.0 and below, we will continue to use legacy way.
#if NET7_0_OR_GREATER
        // TODO: Check with .NET team to see if this can be prevented
        // as this allows user to override the ActivitySource.
        var activitySourceService = serviceProvider?.GetService<ActivitySource>();
        if (activitySourceService != null)
        {
            builder.AddSource(activitySourceService.Name);
        }
        else
        {
            // For users not using hosting package?
            builder.AddSource(HttpInListener.AspNetCoreActivitySourceName);
        }
#else
        builder.AddSource(HttpInListener.ActivitySourceName);
        builder.AddLegacySource(HttpInListener.ActivityOperationName); // for the activities created by AspNetCore
#endif
    }
}
