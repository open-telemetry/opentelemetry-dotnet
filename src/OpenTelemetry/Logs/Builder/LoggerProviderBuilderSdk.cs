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

#nullable enable

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

using CallbackHelper = OpenTelemetry.ProviderBuilderServiceCollectionCallbackHelper<
    OpenTelemetry.Logs.LoggerProviderBuilderSdk,
    OpenTelemetry.Logs.LoggerProviderSdk,
    OpenTelemetry.Logs.LoggerProviderBuilderState>;

namespace OpenTelemetry.Logs;

internal sealed class LoggerProviderBuilderSdk : LoggerProviderBuilder, IDeferredLoggerProviderBuilder
{
    internal readonly LoggerProviderBuilderState? State;

    private readonly bool ownsServices;
    private IServiceCollection? services;

    // This ctor is for a builder created from LoggerProviderBuilderState which
    // happens after the service provider has been created.
    public LoggerProviderBuilderSdk(LoggerProviderBuilderState state)
    {
        Debug.Assert(state != null, "state was null");

        this.State = state;
    }

    // This ctor is for ILoggingBuilder.AddOpenTelemetry scenario where the
    // builder is bound to an external service collection.
    public LoggerProviderBuilderSdk(IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null");

        services.AddOptions();
        services.TryAddSingleton<LoggerProvider>(sp => new LoggerProviderSdk(sp, ownsServiceProvider: false));

        this.services = services;
        this.ownsServices = false;
    }

    // This ctor is for Sdk.CreateLoggerProviderBuilder where the builder
    // owns its services and service provider.
    public LoggerProviderBuilderSdk()
    {
        var services = new ServiceCollection();

        services.AddOptions();

        this.services = services;
        this.ownsServices = true;
    }

    /// <inheritdoc />
    LoggerProviderBuilder IDeferredLoggerProviderBuilder.Configure(
        Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        if (this.State != null)
        {
            configure(this.State.ServiceProvider, this);
        }
        else
        {
            this.ConfigureServices(services
                => CallbackHelper.RegisterConfigureBuilderCallback(services, configure));
        }

        return this;
    }

    public LoggerProviderBuilder AddExporter<T>(ExportProcessorType exportProcessorType, string? name, Action<ExportLogRecordProcessorOptions>? configure)
        where T : BaseExporter<LogRecord>
    {
        this.TryAddSingleton<T>();
        this.ConfigureState((sp, state)
            => state.AddProcessor(
                BuildExportProcessor(state.ServiceProvider, exportProcessorType, sp.GetRequiredService<T>(), name, configure)));

        return this;
    }

    public LoggerProviderBuilder AddExporter(ExportProcessorType exportProcessorType, BaseExporter<LogRecord> exporter, string? name, Action<ExportLogRecordProcessorOptions>? configure)
    {
        Guard.ThrowIfNull(exporter);

        this.ConfigureState((sp, state)
            => state.AddProcessor(
                BuildExportProcessor(state.ServiceProvider, exportProcessorType, exporter, name, configure)));

        return this;
    }

    public LoggerProviderBuilder AddInstrumentation<T>(
        Func<IServiceProvider, LoggerProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureState((sp, state)
            => state.AddInstrumentation(
                typeof(T).Name,
                "semver:" + typeof(T).Assembly.GetName().Version,
                instrumentationFactory(sp, state.Provider)));

        return this;
    }

    public LoggerProviderBuilder AddInstrumentation<T>()
        where T : class
    {
        this.TryAddSingleton<T>();
        this.AddInstrumentation((sp, provider) => sp.GetRequiredService<T>());

        return this;
    }

    public LoggerProviderBuilder AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Guard.ThrowIfNull(processor);

        return this.ConfigureState((sp, state) => state.AddProcessor(processor));
    }

    public LoggerProviderBuilder AddProcessor<T>()
        where T : BaseProcessor<LogRecord>
    {
        this.TryAddSingleton<T>();
        this.ConfigureState((sp, state) => state.AddProcessor(sp.GetRequiredService<T>()));

        return this;
    }

    public LoggerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return this.ConfigureState((sp, state) => state.ConfigureResource(configure));
    }

    public LoggerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
        }

        configure(services);

        return this;
    }

    public LoggerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);

        return this.ConfigureState((sp, state) => state.SetResourceBuilder(resourceBuilder));
    }

    public LoggerProvider Build()
    {
        if (!this.ownsServices || this.State != null)
        {
            throw new NotSupportedException("Build cannot be called directly on LoggerProviderBuilder tied to external services.");
        }

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("LoggerProviderBuilder build method cannot be called multiple times.");
        }

        this.services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new LoggerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }

    private static BaseProcessor<LogRecord> BuildExportProcessor(
        IServiceProvider serviceProvider,
        ExportProcessorType exportProcessorType,
        BaseExporter<LogRecord> exporter,
        string? name,
        Action<ExportLogRecordProcessorOptions>? configure)
    {
        name ??= Options.DefaultName;

        switch (exportProcessorType)
        {
            case ExportProcessorType.Simple:
                return new SimpleLogRecordExportProcessor(exporter);
            case ExportProcessorType.Batch:
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<ExportLogRecordProcessorOptions>>().Get(name);

                options.ExportProcessorType = ExportProcessorType.Batch;

                configure?.Invoke(options);

                var batchOptions = options.BatchExportProcessorOptions;

                return new BatchLogRecordExportProcessor(
                    exporter,
                    batchOptions.MaxQueueSize,
                    batchOptions.ScheduledDelayMilliseconds,
                    batchOptions.ExporterTimeoutMilliseconds,
                    batchOptions.MaxExportBatchSize);
            default:
                throw new NotSupportedException($"ExportProcessorType '{exportProcessorType}' is not supported.");
        }
    }

    private LoggerProviderBuilder ConfigureState(Action<IServiceProvider, LoggerProviderBuilderState> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        if (this.State != null)
        {
            configure!(this.State.ServiceProvider, this.State);
        }
        else
        {
            this.ConfigureServices(services
                => CallbackHelper.RegisterConfigureStateCallback(services, configure!));
        }

        return this;
    }

    private void TryAddSingleton<T>()
        where T : class
    {
        var services = this.services;

        services?.TryAddSingleton<T>();
    }
}

