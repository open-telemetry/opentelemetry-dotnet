// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET11_0_OR_GREATER
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Registers an <see cref="ActivityListenerBuilder"/> so that <see cref="ActivitySource"/>s enabled
/// through <see cref="ITracingBuilder"/> are observed by the OpenTelemetry SDK.
/// </summary>
internal static class OpenTelemetryTracingListener
{
    internal const string ListenerName = "OpenTelemetry";

    internal static void RegisterTracingListener(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        // Note: Unlike IMetricsListener, ActivityListenerBuilder registrations cannot be
        // de-duplicated with TryAddEnumerable, so a marker service is used to ensure the
        // listener is only registered once per IServiceCollection.
        if (services.Any(static service => service.ServiceType == typeof(RegistrationMarker)))
        {
            return;
        }

        services.TryAddSingleton<RegistrationMarker>();

        new TracingBuilder(services).AddListener(
            ListenerName,
            static (serviceProvider, builder) => Configure(serviceProvider.GetRequiredService<TracerProvider>(), builder));
    }

    internal static void Configure(TracerProvider tracerProvider, ActivityListenerBuilder builder)
    {
        if (tracerProvider is not TracerProviderSdk tracerProviderSdk)
        {
            return;
        }

        // Programmatic calls adding sources to the SDK (either by calling AddSource directly or
        // indirectly through helpers such as 'AddInstrumentation' extensions) are always favored
        // over external registrations, so that an Activity is never processed twice by the same provider.
        builder.Sample = (ref options) => tracerProviderSdk.IsListeningTo(options.Source) ?
            ActivitySamplingResult.None :
            tracerProviderSdk.OnSample(ref options);

        builder.ActivityStarted = activity =>
        {
            if (!tracerProviderSdk.IsListeningTo(activity.Source))
            {
                tracerProviderSdk.OnActivityStarted(activity);
            }
        };

        builder.ActivityStopped = activity =>
        {
            if (!tracerProviderSdk.IsListeningTo(activity.Source))
            {
                tracerProviderSdk.OnActivityStopped(activity);
            }
        };
    }

    private sealed class RegistrationMarker;

    private sealed class TracingBuilder(IServiceCollection services) : ITracingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
#endif
