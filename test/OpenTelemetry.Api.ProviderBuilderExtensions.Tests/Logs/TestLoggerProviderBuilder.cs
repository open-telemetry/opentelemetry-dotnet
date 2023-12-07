// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Api.ProviderBuilderExtensions.Tests;

internal sealed class TestLoggerProviderBuilder : LoggerProviderBuilder, ILoggerProviderBuilder, IDisposable
{
    public TestLoggerProviderBuilder()
    {
        this.Services = new ServiceCollection();
    }

    public IServiceCollection? Services { get; private set; }

    public ServiceProvider? ServiceProvider { get; private set; }

    public List<object> Instrumentation { get; } = new();

    public LoggerProvider? Provider { get; private set; }

    public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
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

    public LoggerProviderBuilder ConfigureBuilder(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        var services = this.Services;
        if (services != null)
        {
            services.ConfigureOpenTelemetryLoggerProvider(configure);
        }
        else
        {
            var serviceProvider = this.ServiceProvider ?? throw new InvalidOperationException("Test failure");
            configure(serviceProvider, this);
        }

        return this;
    }

    public LoggerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
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

        this.Provider = new NoopLoggerProvider();

        return this.ServiceProvider = services.BuildServiceProvider();
    }

    public int InvokeRegistrations()
    {
        var serviceProvider = this.ServiceProvider ?? throw new InvalidOperationException();

        var registrations = serviceProvider.GetServices<IConfigureLoggerProviderBuilder>();

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

    LoggerProviderBuilder IDeferredLoggerProviderBuilder.Configure(Action<IServiceProvider, LoggerProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    private sealed class NoopLoggerProvider : LoggerProvider
    {
    }
}