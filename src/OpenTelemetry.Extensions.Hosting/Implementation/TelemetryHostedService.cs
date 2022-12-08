// <copyright file="TelemetryHostedService.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

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
        try
        {
            // The sole purpose of this HostedService is to ensure all
            // instrumentations, exporters, etc., are created and started.
            Initialize(this.serviceProvider);
        }
        catch (Exception ex)
        {
            HostingExtensionsEventSource.Log.FailedOpenTelemetrySDK(ex);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static void Initialize(IServiceProvider serviceProvider)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");

        var meterProvider = serviceProvider.GetService<MeterProvider>();
        var tracerProvider = serviceProvider.GetService<TracerProvider>();

        if (meterProvider == null && tracerProvider == null)
        {
            throw new InvalidOperationException("Could not resolve either MeterProvider or TracerProvider through application ServiceProvider, OpenTelemetry SDK has not been initialized.");
        }
    }
}
