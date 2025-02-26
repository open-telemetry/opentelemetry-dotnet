// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Api.ProviderBuilderExtensions.Tests;

internal sealed class TestTracerProviderBuilder : TracerProviderBuilder, ITracerProviderBuilder, IDisposable
{
    public TestTracerProviderBuilder()
    {
        this.Services = new ServiceCollection();
    }

    public IServiceCollection? Services { get; private set; }

    public ServiceProvider? ServiceProvider { get; private set; }

    public List<string> Sources { get; } = new();

    public List<string> LegacySources { get; } = new();

    public List<object> Instrumentation { get; } = new();

    public TracerProvider? Provider { get; private set; }

    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        if (this.Services != null)
        {
            this.ConfigureBuilder((sp, builder) => builder.AddInstrumentation(instrumentationFactory));
        }
        else
        {
            var instrumentation = instrumentationFactory();
            if (instrumentation is not null)
            {
                this.Instrumentation.Add(instrumentation);
            }
        }

        return this;
    }

    public override TracerProviderBuilder AddLegacySource(string operationName)
    {
        if (this.Services != null)
        {
            this.ConfigureBuilder((sp, builder) => builder.AddLegacySource(operationName));
        }
        else
        {
            this.LegacySources.Add(operationName);
        }

        return this;
    }

    public override TracerProviderBuilder AddSource(params string[] names)
    {
        if (this.Services != null)
        {
            this.ConfigureBuilder((sp, builder) => builder.AddSource(names));
        }
        else
        {
            foreach (string name in names)
            {
                this.Sources.Add(name);
            }
        }

        return this;
    }

    public TracerProviderBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        var services = this.Services;
        if (services != null)
        {
            services.ConfigureOpenTelemetryTracerProvider(configure);
        }
        else
        {
            var serviceProvider = this.ServiceProvider ?? throw new InvalidOperationException("Test failure");
            configure(serviceProvider, this);
        }

        return this;
    }

    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        var services = this.Services;
        if (services != null)
        {
            configure(services);
        }
        else
        {
            throw new NotSupportedException("Services cannot be configured after the ServiceProvider has been created.");
        }

        return this;
    }

    public IServiceProvider BuildServiceProvider()
    {
        var services = this.Services ?? throw new InvalidOperationException();

        this.Services = null;

        this.Provider = new NoopTracerProvider();

        return this.ServiceProvider = services.BuildServiceProvider();
    }

    public int InvokeRegistrations()
    {
        var serviceProvider = this.ServiceProvider ?? throw new InvalidOperationException();

        var registrations = serviceProvider.GetServices<IConfigureTracerProviderBuilder>();

        var count = 0;

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, this);
            count++;
        }

        return count;
    }

    public void Dispose()
    {
        this.ServiceProvider?.Dispose();
    }

    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    private sealed class NoopTracerProvider : TracerProvider
    {
    }
}
