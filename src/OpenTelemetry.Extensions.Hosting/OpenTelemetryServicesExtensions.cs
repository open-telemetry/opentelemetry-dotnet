// <copyright file="OpenTelemetryServicesExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up OpenTelemetry services in an <see
/// cref="IServiceCollection" />.
/// </summary>
public static class OpenTelemetryServicesExtensions
{
    /// <summary>
    /// Adds OpenTelemetry SDK services into the supplied <see
    /// cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> and/or <see
    /// cref="MeterProvider"/> will be created for a given <see
    /// cref="IServiceCollection"/>.
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static OpenTelemetryBuilder AddOpenTelemetry(this IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        if (!services.Any((ServiceDescriptor d) => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TelemetryHostedService)))
        {
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
        }

        return new(services);
    }
}
