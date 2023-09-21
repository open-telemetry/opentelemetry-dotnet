// <copyright file="LoggerProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
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
    private LoggerProviderSdk? loggerProvider;

    public LoggerProviderBuilderSdk(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public List<InstrumentationRegistration> Instrumentation { get; } = new();

    public ResourceBuilder? ResourceBuilder { get; private set; }

    public LoggerProvider? Provider => this.loggerProvider;

    public List<BaseProcessor<LogRecord>> Processors { get; } = new();

    public void RegisterProvider(LoggerProviderSdk loggerProvider)
    {
        Debug.Assert(loggerProvider != null, "loggerProvider was null");

        if (this.loggerProvider != null)
        {
            throw new NotSupportedException("LoggerProvider cannot be accessed while build is executing.");
        }

        this.loggerProvider = loggerProvider;
    }

    public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation?> instrumentationFactory)
        where TInstrumentation : class
    {
        Debug.Assert(instrumentationFactory != null, "instrumentationFactory was null");

        this.Instrumentation.Add(
            new InstrumentationRegistration(
                typeof(TInstrumentation).Name,
                typeof(TInstrumentation).Assembly.GetName().Version?.ToString() ?? DefaultInstrumentationVersion,
                instrumentationFactory!()));

        return this;
    }

    public LoggerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure!(resourceBuilder);

        return this;
    }

    public LoggerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Debug.Assert(resourceBuilder != null, "resourceBuilder was null");

        this.ResourceBuilder = resourceBuilder;

        return this;
    }

    public LoggerProviderBuilder AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Debug.Assert(processor != null, "processor was null");

        this.Processors.Add(processor!);

        return this;
    }

    public LoggerProviderBuilder ConfigureBuilder(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        configure!(this.serviceProvider, this);

        return this;
    }

    public LoggerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
    }

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
