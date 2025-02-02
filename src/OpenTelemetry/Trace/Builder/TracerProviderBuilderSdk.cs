// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace;

/// <summary>
/// Stores state used to build a <see cref="TracerProvider"/>.
/// </summary>
internal sealed class TracerProviderBuilderSdk : TracerProviderBuilder, ITracerProviderBuilder
{
    private const string DefaultInstrumentationVersion = "1.0.0.0";

    private readonly IServiceProvider serviceProvider;
    private TracerProviderSdk? tracerProvider;

    public TracerProviderBuilderSdk(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public List<InstrumentationRegistration> Instrumentation { get; } = new();

    public ResourceBuilder? ResourceBuilder { get; private set; }

    public TracerProvider? Provider => this.tracerProvider;

    public List<BaseProcessor<Activity>> Processors { get; } = new();

    public List<string> Sources { get; } = new();

    public HashSet<string> LegacyActivityOperationNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Sampler? Sampler { get; private set; }

    public bool ExceptionProcessorEnabled { get; private set; }

    public void RegisterProvider(TracerProviderSdk tracerProvider)
    {
        if (this.tracerProvider != null)
        {
            throw new NotSupportedException("TracerProvider cannot be accessed while build is executing.");
        }

        this.tracerProvider = tracerProvider;
    }

    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        return this.AddInstrumentation(
            typeof(TInstrumentation).Name,
            typeof(TInstrumentation).Assembly.GetName().Version?.ToString() ?? DefaultInstrumentationVersion,
            instrumentationFactory());
    }

    public TracerProviderBuilder AddInstrumentation(
        string instrumentationName,
        string instrumentationVersion,
        object? instrumentation)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationName), "instrumentationName was null or whitespace");
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationVersion), "instrumentationVersion was null or whitespace");

        this.Instrumentation.Add(
            new InstrumentationRegistration(
                instrumentationName,
                instrumentationVersion,
                instrumentation));

        return this;
    }

    public TracerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure(resourceBuilder);

        return this;
    }

    public TracerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        this.ResourceBuilder = resourceBuilder;

        return this;
    }

    public override TracerProviderBuilder AddLegacySource(string operationName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operationName), "operationName was null or whitespace");

        this.LegacyActivityOperationNames.Add(operationName);

        return this;
    }

    public override TracerProviderBuilder AddSource(params string[] names)
    {
        foreach (var name in names)
        {
            Guard.ThrowIfNullOrWhitespace(name);

            // TODO: We need to fix the listening model.
            // Today it ignores version.
            this.Sources.Add(name);
        }

        return this;
    }

    public TracerProviderBuilder AddProcessor(BaseProcessor<Activity> processor)
    {
        this.Processors.Add(processor);

        return this;
    }

    public TracerProviderBuilder SetSampler(Sampler sampler)
    {
        this.Sampler = sampler;

        return this;
    }

    public TracerProviderBuilder SetErrorStatusOnException(bool enabled)
    {
        this.ExceptionProcessorEnabled = enabled;

        return this;
    }

    public TracerProviderBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        configure(this.serviceProvider, this);

        return this;
    }

    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
    }

    public void AddExceptionProcessorIfEnabled(ref IEnumerable<BaseProcessor<Activity>> processors)
    {
        if (this.ExceptionProcessorEnabled)
        {
            try
            {
                processors = new BaseProcessor<Activity>[] { new ExceptionProcessor() }.Concat(processors);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException($"'{nameof(TracerProviderBuilderExtensions.SetErrorStatusOnException)}' is not supported on this platform", ex);
            }
        }
    }

    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
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
