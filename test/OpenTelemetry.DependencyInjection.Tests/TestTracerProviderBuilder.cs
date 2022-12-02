// <copyright file="TestTracerProviderBuilder.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.DependencyInjection.Tests;

public sealed class TestTracerProviderBuilder : TracerProviderBuilder, ITracerProviderBuilder
{
    public TestTracerProviderBuilder()
    {
        this.Services = new ServiceCollection();
    }

    public IServiceCollection Services { get; }

    public TracerProvider? Provider => null;

    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        => this.ConfigureBuilder((sp, builder) => builder.AddInstrumentation(instrumentationFactory));

    public override TracerProviderBuilder AddLegacySource(string operationName)
        => this.ConfigureBuilder((sp, builder) => builder.AddLegacySource(operationName));

    public override TracerProviderBuilder AddSource(params string[] names)
        => this.ConfigureBuilder((sp, builder) => builder.AddSource(names));

    public TracerProviderBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        this.Services.ConfigureOpenTelemetryTracerProvider(configure);

        return this;
    }

    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(this.Services);

        return this;
    }

    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilder(configure);
}
