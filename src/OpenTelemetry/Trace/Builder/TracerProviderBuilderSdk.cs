// <copyright file="TracerProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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
        Debug.Assert(tracerProvider != null, "tracerProvider was null");

        if (this.tracerProvider != null)
        {
            throw new NotSupportedException("TracerProvider cannot be accessed while build is executing.");
        }

        this.tracerProvider = tracerProvider;
    }

    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
    {
        Debug.Assert(instrumentationFactory != null, "instrumentationFactory was null");

        return this.AddInstrumentation(
            typeof(TInstrumentation).Name,
            typeof(TInstrumentation).Assembly.GetName().Version?.ToString() ?? DefaultInstrumentationVersion,
            instrumentationFactory!());
    }

    public TracerProviderBuilder AddInstrumentation(
        string instrumentationName,
        string instrumentationVersion,
        object instrumentation)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationName), "instrumentationName was null or whitespace");
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationVersion), "instrumentationVersion was null or whitespace");
        Debug.Assert(instrumentation != null, "instrumentation was null");

        this.Instrumentation.Add(
            new InstrumentationRegistration(
                instrumentationName,
                instrumentationVersion,
                instrumentation!));

        return this;
    }

    public TracerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure!(resourceBuilder);

        return this;
    }

    public TracerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Debug.Assert(resourceBuilder != null, "resourceBuilder was null");

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
        Debug.Assert(names != null, "names was null");

        foreach (var name in names!)
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
        Debug.Assert(processor != null, "processor was null");

        this.Processors.Add(processor!);

        return this;
    }

    public TracerProviderBuilder SetSampler(Sampler sampler)
    {
        Debug.Assert(sampler != null, "sampler was null");

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
        Debug.Assert(configure != null, "configure was null");

        configure!(this.serviceProvider, this);

        return this;
    }

    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
    }

    public void AddExceptionProcessorIfEnabled()
    {
        if (this.ExceptionProcessorEnabled)
        {
            try
            {
                this.Processors.Insert(0, new ExceptionProcessor());
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
        public readonly object Instance;

        internal InstrumentationRegistration(string name, string version, object instance)
        {
            this.Name = name;
            this.Version = version;
            this.Instance = instance;
        }
    }
}
