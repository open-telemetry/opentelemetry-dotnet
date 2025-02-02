// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Warning: Do not change the namespace or class name in this file! Azure
// Functions has taken a dependency on the specific details:
// https://github.com/Azure/azure-functions-host/blob/d4655cc4fbb34fc14e6861731991118a9acd02ed/src/WebJobs.Script.WebHost/DependencyInjection/DependencyValidator/DependencyValidator.cs#L57

namespace OpenTelemetry.Extensions.Hosting.Implementation;

internal sealed class TelemetryHostedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;

    public TelemetryHostedService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // The sole purpose of this HostedService is to ensure all
        // instrumentations, exporters, etc., are created and started.
        Initialize(this.serviceProvider);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static void Initialize(IServiceProvider serviceProvider)
    {
        var meterProvider = serviceProvider.GetService<MeterProvider>();
        if (meterProvider == null)
        {
            HostingExtensionsEventSource.Log.MeterProviderNotRegistered();
        }

        var tracerProvider = serviceProvider.GetService<TracerProvider>();
        if (tracerProvider == null)
        {
            HostingExtensionsEventSource.Log.TracerProviderNotRegistered();
        }

        var loggerProvider = serviceProvider.GetService<LoggerProvider>();
        if (loggerProvider == null)
        {
            HostingExtensionsEventSource.Log.LoggerProviderNotRegistered();
        }
    }
}
