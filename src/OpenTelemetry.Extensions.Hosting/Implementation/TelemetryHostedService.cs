// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

// Warning: Do not change the namespace or class name in this file! Azure
// Functions has taken a dependency on the specific details:
// https://github.com/Azure/azure-functions-host/blob/d4655cc4fbb34fc14e6861731991118a9acd02ed/src/WebJobs.Script.WebHost/DependencyInjection/DependencyValidator/DependencyValidator.cs#L57

namespace OpenTelemetry.Extensions.Hosting.Implementation;

internal sealed class TelemetryHostedService(ITelemetryHostInitializer initializer) : IHostedLifecycleService
{
    private bool initialized;

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        // Initialize as early as possible so that telemetry emitted by other
        // services during IHostedLifecycleService.StartingAsync is captured.
        this.Initialize();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fallback for hosts which do not invoke the IHostedLifecycleService
        // methods.
        this.Initialize();
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void Initialize()
    {
        if (!this.initialized)
        {
            initializer.Initialize();
            this.initialized = true;
        }
    }
}
