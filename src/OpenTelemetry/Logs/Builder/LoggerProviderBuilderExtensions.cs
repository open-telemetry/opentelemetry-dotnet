// <copyright file="LoggerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods for the <see cref="LoggerProviderBuilder"/> class.
/// </summary>
public static class LoggerProviderBuilderExtensions
{
    /// <summary>
    /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from. Overwrites currently set ResourceBuilder.
    /// You should usually use <see cref="ConfigureResource(LoggerProviderBuilder, Action{ResourceBuilder})"/> instead
    /// (call <see cref="ResourceBuilder.Clear"/> if desired).
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder SetResourceBuilder(this LoggerProviderBuilder loggerProviderBuilder, ResourceBuilder resourceBuilder)
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.SetResourceBuilder(resourceBuilder);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from in-place.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder ConfigureResource(this LoggerProviderBuilder loggerProviderBuilder, Action<ResourceBuilder> configure)
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.ConfigureResource(configure);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Adds a processor to the provider.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="processor">Activity processor to add.</param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddProcessor(this LoggerProviderBuilder loggerProviderBuilder, BaseProcessor<LogRecord> processor)
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.AddProcessor(processor);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Adds a processor to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Processor type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddProcessor<T>(this LoggerProviderBuilder loggerProviderBuilder)
        where T : BaseProcessor<LogRecord>
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.AddProcessor<T>();
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Adds an exporter to the provider.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
    /// <param name="exporter">LogRecord exporter to add.</param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        ExportProcessorType exportProcessorType,
        BaseExporter<LogRecord> exporter)
        => AddExporter(loggerProviderBuilder, exportProcessorType, exporter, name: null, configure: null);

    /// <summary>
    /// Adds an exporter to the provider.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
    /// <param name="exporter">LogRecord exporter to add.</param>
    /// <param name="configure"><inheritdoc cref="AddExporter{T}(LoggerProviderBuilder, ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/param[@name='configure']"/></param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        ExportProcessorType exportProcessorType,
        BaseExporter<LogRecord> exporter,
        Action<ExportLogRecordProcessorOptions> configure)
        => AddExporter(loggerProviderBuilder, exportProcessorType, exporter, name: null, configure);

    /// <summary>
    /// Adds an exporter to the provider.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
    /// <param name="exporter">LogRecord exporter to add.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure"><inheritdoc cref="AddExporter{T}(LoggerProviderBuilder, ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/param[@name='configure']"/></param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        ExportProcessorType exportProcessorType,
        BaseExporter<LogRecord> exporter,
        string? name,
        Action<ExportLogRecordProcessorOptions>? configure)
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.AddExporter(exportProcessorType, exporter, name, configure);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Adds an exporter to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <remarks><inheritdoc cref="AddExporter{T}(LoggerProviderBuilder, ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/remarks"/></remarks>
    /// <typeparam name="T">Exporter type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddExporter<T>(
        this LoggerProviderBuilder loggerProviderBuilder,
        ExportProcessorType exportProcessorType)
        where T : BaseExporter<LogRecord>
        => AddExporter<T>(loggerProviderBuilder, exportProcessorType, name: null, configure: null);

    /// <summary>
    /// Adds an exporter to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <remarks><inheritdoc cref="AddExporter{T}(LoggerProviderBuilder, ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/remarks"/></remarks>
    /// <typeparam name="T">Exporter type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
    /// <param name="configure"><inheritdoc cref="AddExporter{T}(LoggerProviderBuilder, ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/param[@name='configure']"/></param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddExporter<T>(
        this LoggerProviderBuilder loggerProviderBuilder,
        ExportProcessorType exportProcessorType,
        Action<ExportLogRecordProcessorOptions> configure)
        where T : BaseExporter<LogRecord>
        => AddExporter<T>(loggerProviderBuilder, exportProcessorType, name: null, configure);

    /// <summary>
    /// Adds an exporter to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Exporter type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action to configure <see
    /// cref="ExportLogRecordProcessorOptions"/>. Only invoked when <paramref
    /// name="exportProcessorType"/> is <see
    /// cref="ExportProcessorType.Batch"/>.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddExporter<T>(
        this LoggerProviderBuilder loggerProviderBuilder,
        ExportProcessorType exportProcessorType,
        string? name,
        Action<ExportLogRecordProcessorOptions>? configure)
        where T : BaseExporter<LogRecord>
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.AddExporter<T>(exportProcessorType, name, configure);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddInstrumentation<T>(this LoggerProviderBuilder loggerProviderBuilder)
        where T : class
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.AddInstrumentation<T>();
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddInstrumentation<T>(
        this LoggerProviderBuilder loggerProviderBuilder,
        Func<IServiceProvider, LoggerProvider, T> instrumentationFactory)
        where T : class
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.AddInstrumentation(instrumentationFactory);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where tracing services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Tracing services are only available during the application
    /// configuration phase.
    /// </remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder ConfigureServices(
        this LoggerProviderBuilder loggerProviderBuilder,
        Action<IServiceCollection> configure)
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            loggerProviderBuilderSdk.ConfigureServices(configure);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="LoggerProviderBuilder"/> once the application <see
    /// cref="IServiceProvider"/> is available.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder ConfigureBuilder(
        this LoggerProviderBuilder loggerProviderBuilder,
        Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        if (loggerProviderBuilder is IDeferredLoggerProviderBuilder deferredLoggerProviderBuilder)
        {
            deferredLoggerProviderBuilder.Configure(configure);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Run the given actions to initialize the <see cref="LoggerProvider"/>.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns><see cref="LoggerProvider"/>.</returns>
    public static LoggerProvider Build(this LoggerProviderBuilder loggerProviderBuilder)
    {
        if (loggerProviderBuilder is LoggerProviderBuilderSdk loggerProviderBuilderSdk)
        {
            return loggerProviderBuilderSdk.Build();
        }

        return new NoopLoggerProvider();
    }
}
