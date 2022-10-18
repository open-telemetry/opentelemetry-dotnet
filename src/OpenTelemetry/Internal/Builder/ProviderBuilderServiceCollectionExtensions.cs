// <copyright file="ProviderBuilderServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

internal static class ProviderBuilderServiceCollectionExtensions
{
    public static IServiceCollection AddOpenTelemetryMeterProviderBuilderServices(this IServiceCollection services)
    {
        services.AddOpenTelemetryProviderBuilderServices();

        services.RegisterOptionsFactory(configuration => new MetricReaderOptions(configuration));

        return services;
    }

    public static IServiceCollection AddOpenTelemetryTracerProviderBuilderServices(this IServiceCollection services)
    {
        services.AddOpenTelemetryProviderBuilderServices();

        services.RegisterOptionsFactory(configuration => new ExportActivityProcessorOptions(configuration));

        return services;
    }

    private static IServiceCollection AddOpenTelemetryProviderBuilderServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null");

        services.AddOptions();

        // Note: When using a host builder IConfiguration is automatically
        // registered and this registration will no-op. This only runs for
        // Sdk.Create* style or when manually creating a ServiceCollection. The
        // point of this registration is to make IConfiguration available in
        // those cases.
        services!.TryAddSingleton<IConfiguration>(sp => new ConfigurationBuilder().AddEnvironmentVariables().Build());

        return services!;
    }
}
