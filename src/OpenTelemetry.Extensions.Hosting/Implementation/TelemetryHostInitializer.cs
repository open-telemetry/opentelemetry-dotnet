// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Hosting.Implementation;

internal sealed class TelemetryHostInitializer(IServiceProvider serviceProvider) : ITelemetryHostInitializer
{
    public void Initialize()
    {
        if (serviceProvider.GetService<MeterProvider>() is null)
        {
            HostingExtensionsEventSource.Log.MeterProviderNotRegistered();
        }

        if (serviceProvider.GetService<TracerProvider>() is null)
        {
            HostingExtensionsEventSource.Log.TracerProviderNotRegistered();
        }

        if (serviceProvider.GetService<LoggerProvider>() is null)
        {
            HostingExtensionsEventSource.Log.LoggerProviderNotRegistered();
        }
    }
}
