// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Api.ProviderBuilderExtensions.Tests;

internal sealed class TestMeterProviderBuilder : MeterProviderBuilder, IMeterProviderBuilder, IDisposable
{
    public TestMeterProviderBuilder()
    {
        this.Services = new ServiceCollection();
    }

    public IServiceCollection? Services { get; private set; }

    public ServiceProvider? ServiceProvider { get; private set; }

    public List<string> Meters { get; } = new();

    public List<object> Instrumentation { get; } = new();

    public MeterProvider? Provider { get; private set; }

    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
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

    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        if (this.Services != null)
        {
            this.ConfigureBuilder((sp, builder) => builder.AddMeter(names));
        }
        else
        {
            foreach (string name in names)
            {
                this.Meters.Add(name);
            }
        }

        return this;
    }

    public MeterProviderBuilder ConfigureBuilder(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        var services = this.Services;
        if (services != null)
        {
            services.ConfigureOpenTelemetryMeterProvider(configure);
        }
        else
        {
            var serviceProvider = this.ServiceProvider ?? throw new InvalidOperationException("Test failure");
            configure(serviceProvider, this);
        }

        return this;
    }

    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
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

        this.Provider = new NoopMeterProvider();

        return this.ServiceProvider = services.BuildServiceProvider();
    }

    public int InvokeRegistrations()
    {
        var serviceProvider = this.ServiceProvider ?? throw new InvalidOperationException();

        var registrations = serviceProvider.GetServices<IConfigureMeterProviderBuilder>();

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

    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    private sealed class NoopMeterProvider : MeterProvider
    {
    }
}
