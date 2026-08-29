// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

// Warning: Do not change the namespace or class name in this file! Azure
// Functions has taken a dependency on the specific details:
// https://github.com/Azure/azure-functions-host/blob/d4655cc4fbb34fc14e6861731991118a9acd02ed/src/WebJobs.Script.WebHost/DependencyInjection/DependencyValidator/DependencyValidator.cs#L57

namespace OpenTelemetry.Extensions.Hosting.Implementation;

internal sealed class TelemetryHostedService(ITelemetryHostInitializer initializer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // The sole purpose of this HostedService is to ensure all
        // instrumentations, exporters, etc., are created and started.
        initializer.Initialize();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
