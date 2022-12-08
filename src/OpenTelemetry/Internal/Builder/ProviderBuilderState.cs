// <copyright file="ProviderBuilderState.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry;

internal abstract class ProviderBuilderState<TBuilder, TProvider>
{
    private TProvider? provider;

    protected ProviderBuilderState(IServiceProvider serviceProvider)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");

        this.ServiceProvider = serviceProvider!;
    }

    public IServiceProvider ServiceProvider { get; }

    public abstract TBuilder Builder { get; }

    public TProvider Provider
    {
        get => this.provider ?? throw new InvalidOperationException("Provider has not been set on state.");
    }

    public List<InstrumentationRegistration> Instrumentation { get; } = new();

    public ResourceBuilder? ResourceBuilder { get; protected set; }

    public void RegisterProvider(string providerTypeName, TProvider provider)
    {
        Debug.Assert(provider != null, "provider was null");

        if (this.provider != null)
        {
            throw new NotSupportedException($"{providerTypeName} cannot be accessed while build is executing.");
        }

        this.provider = provider;
    }

    public void AddInstrumentation(
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
    }

    public void ConfigureResource(Action<ResourceBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure!(resourceBuilder);
    }

    public void SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Debug.Assert(resourceBuilder != null, "resourceBuilder was null");

        this.ResourceBuilder = resourceBuilder;
    }

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
