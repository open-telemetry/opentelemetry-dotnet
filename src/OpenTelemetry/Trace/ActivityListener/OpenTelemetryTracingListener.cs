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
        // de-duplicated with TryAddEnumerable, so the bridge doubles as a marker service
        // to ensure the listener is only registered once per IServiceCollection.
        if (services.Any(static service => service.ServiceType == typeof(TracerProviderBridge)))
        {
            return;
        }

        services.TryAddSingleton<TracerProviderBridge>();

        // The listener is bound to the bridge rather than to the TracerProvider so that creating the
        // ActivitySourceFactory never needs to resolve the TracerProvider (which may in turn depend on
        // the factory through a processor or instrumentation), and so that the callbacks can be
        // deactivated when the provider is shut down or disposed.
        new TracingBuilder(services).AddListener(
            ListenerName,
            static (serviceProvider, builder) => Configure(serviceProvider.GetRequiredService<TracerProviderBridge>(), builder));
    }

    /// <summary>
    /// Connects the <paramref name="tracerProvider"/> to the listener registered by
    /// <see cref="RegisterTracingListener"/>, if there is one, and ensures the listener is created.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> the provider was resolved from.</param>
    /// <param name="tracerProvider">The <see cref="TracerProviderSdk"/> to connect.</param>
    /// <returns>The bridge the provider was attached to, if any.</returns>
    internal static TracerProviderBridge? Attach(IServiceProvider serviceProvider, TracerProviderSdk tracerProvider)
    {
        if (serviceProvider.GetService<TracerProviderBridge>() is not { } bridge)
        {
            return null;
        }

        bridge.Attach(tracerProvider);

        // The listener is owned by the ActivitySourceFactory, so it is only created once the factory is.
        // Resolve it here so that sources enabled through configuration are observed without the application
        // needing to resolve the factory itself (for example, when only ITelemetryHostInitializer is used).
        // Neither this nor the listener resolve the TracerProvider, so there is no dependency cycle.
        try
        {
            _ = serviceProvider.GetService<ActivitySourceFactory>();
        }
        catch
        {
            // The caller never receives the bridge if this fails, so it cannot detach the provider itself.
            bridge.Detach(tracerProvider);
            throw;
        }

        return bridge;
    }

    internal static void Configure(TracerProviderBridge bridge, ActivityListenerBuilder builder)
    {
        // Programmatic calls adding sources to the SDK (either by calling AddSource directly or
        // indirectly through helpers such as 'AddInstrumentation' extensions) are always favored
        // over external registrations, so that an Activity is never processed twice by the same provider.
        builder.Sample = (ref options) => bridge.GetProviderFor(options.Source) is { } provider ?
            provider.OnSample(ref options) :
            ActivitySamplingResult.None;

        builder.ActivityStarted = activity => bridge.GetProviderFor(activity.Source)?.OnActivityStarted(activity);

        builder.ActivityStopped = activity => bridge.GetProviderFor(activity.Source)?.OnActivityStopped(activity);
    }

    /// <summary>
    /// Holds the <see cref="TracerProviderSdk"/> that the listener forwards to for as long as it is active.
    /// </summary>
    internal sealed class TracerProviderBridge
    {
        private TracerProviderSdk? tracerProvider;

        internal void Attach(TracerProviderSdk provider)
            => Volatile.Write(ref this.tracerProvider, provider);

        internal void Detach(TracerProviderSdk provider)
            => Interlocked.CompareExchange(ref this.tracerProvider, null, provider);

        internal TracerProviderSdk? GetProviderFor(ActivitySource source)
        {
            var provider = Volatile.Read(ref this.tracerProvider);

            // Legacy activities (those with no ActivitySource) never go through sampling and always
            // report data as requested, so they are only handled by the provider's own listener
            // when they have been explicitly opted into using AddLegacySource.
            return provider is not null &&
                !string.IsNullOrEmpty(source.Name) &&
                !provider.IsListeningTo(source) ?
                provider : null;
        }
    }

    private sealed class TracingBuilder(IServiceCollection services) : ITracingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
#endif
