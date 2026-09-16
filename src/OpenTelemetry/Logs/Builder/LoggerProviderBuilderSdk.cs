// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores state used to build a <see cref="LoggerProvider"/>.
/// </summary>
internal sealed class LoggerProviderBuilderSdk : LoggerProviderBuilder, ILoggerProviderBuilder
{
    private const string DefaultInstrumentationVersion = "1.0.0.0";

    private readonly IServiceProvider serviceProvider;
    private ExceptionDispatchInfo? providerBuildException;
    private LoggerProviderSdk? loggerProvider;

    public LoggerProviderBuilderSdk(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public List<InstrumentationRegistration> Instrumentation { get; } = [];

    public ResourceBuilder? ResourceBuilder { get; private set; }

    public LoggerProvider? Provider => this.loggerProvider;

    public List<BaseProcessor<LogRecord>> Processors { get; } = [];

    public void RegisterProvider(LoggerProviderSdk loggerProvider)
    {
        this.providerBuildException?.Throw();

        if (this.loggerProvider != null)
        {
            throw new NotSupportedException("LoggerProvider cannot be accessed while build is executing.");
        }

        this.loggerProvider = loggerProvider;
    }

    public void HandleProviderBuildFailure(LoggerProviderSdk loggerProvider, Exception exception)
    {
        if (ReferenceEquals(this.loggerProvider, loggerProvider))
        {
            this.loggerProvider = null;

            // Processors and instrumentations are disposed when provider
            // construction fails. If any were added, retrying would either
            // reuse disposed instances or lose configuration that was
            // transferred destructively from OpenTelemetryLoggerOptions.
            if (this.Processors.Count != 0
                || this.Instrumentation.Count != 0
                || this.ResourceBuilder != null)
            {
                this.providerBuildException = ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    public void ResetBuildState()
    {
        this.Processors.Clear();
        this.Instrumentation.Clear();
        this.ResourceBuilder = null;
    }

    public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        var instance = instrumentationFactory();

        this.Instrumentation.Add(
            new InstrumentationRegistration(
                typeof(TInstrumentation).Name,
                typeof(TInstrumentation).Assembly.GetName().Version?.ToString() ?? DefaultInstrumentationVersion,
                instance));

        return this;
    }

    public LoggerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure(resourceBuilder);

        return this;
    }

    public LoggerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        this.ResourceBuilder = resourceBuilder;
        return this;
    }

    public LoggerProviderBuilder AddProcessor(BaseProcessor<LogRecord> processor)
    {
        this.Processors.Add(processor);
        return this;
    }

    public LoggerProviderBuilder ConfigureBuilder(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        configure(this.serviceProvider, this);
        return this;
    }

    public LoggerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        => throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");

    LoggerProviderBuilder IDeferredLoggerProviderBuilder.Configure(Action<IServiceProvider, LoggerProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    internal readonly struct InstrumentationRegistration
    {
        public readonly string Name;
        public readonly string Version;
        public readonly object? Instance;

        internal InstrumentationRegistration(string name, string version, object? instance)
        {
            this.Name = name;
            this.Version = version;
            this.Instance = instance;
        }
    }
}
