// <copyright file="TracerProviderAutoConfigurationBuilder.cs" company="OpenTelemetry Authors">
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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OpenTelemetry.Trace;

public class TracerProviderAutoConfigurationBuilder
{
    private readonly TracerProviderBuilder tracerProviderBuilder;

    internal TracerProviderAutoConfigurationBuilder(TracerProviderBuilder tracerProviderBuilder)
    {
        this.tracerProviderBuilder = tracerProviderBuilder;
    }

    public TracerProviderAutoConfigurationBuilder AddTraceExporterDetector<T>()
        where T : class, ITraceExporterDetector
    {
        return this.ConfigureServices(services => services.TryAddSingleton<ITraceExporterDetector, T>());
    }

    public TracerProviderAutoConfigurationBuilder AddTraceSamplerDetector<T>()
        where T : class, ITraceSamplerDetector
    {
        return this.ConfigureServices(services => services.TryAddSingleton<ITraceSamplerDetector, T>());
    }

    public TracerProviderAutoConfigurationBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        this.tracerProviderBuilder.ConfigureServices(configure);
        return this;
    }

    public TracerProviderAutoConfigurationBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        this.tracerProviderBuilder.ConfigureBuilder(configure);
        return this;
    }
}
