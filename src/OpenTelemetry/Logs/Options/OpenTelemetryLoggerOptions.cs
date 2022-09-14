// <copyright file="OpenTelemetryLoggerOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Contains OpenTelemetry logging options.
    /// </summary>
    public class OpenTelemetryLoggerOptions
    {
        internal List<Action<IServiceProvider, OpenTelemetryLoggerProvider>>? ConfigurationActions = new();

        private const bool DefaultIncludeScopes = false;
        private const bool DefaultIncludeFormattedMessage = false;
        private const bool DefaultParseStateValues = false;

        private IServiceCollection? services;
        private bool? includeScopes;
        private bool? includeFormattedMessage;
        private bool? parseStateValues;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryLoggerOptions"/> class.
        /// </summary>
        public OpenTelemetryLoggerOptions()
            : this(services: null)
        {
        }

        internal OpenTelemetryLoggerOptions(IServiceCollection? services)
        {
            this.services = services;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not log scopes should be
        /// included on generated <see cref="LogRecord"/>s. Default value:
        /// False.
        /// </summary>
        public bool IncludeScopes
        {
            get => this.includeScopes ?? DefaultIncludeScopes;
            set => this.includeScopes = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not formatted log message
        /// should be included on generated <see cref="LogRecord"/>s. Default
        /// value: False.
        /// </summary>
        public bool IncludeFormattedMessage
        {
            get => this.includeFormattedMessage ?? DefaultIncludeFormattedMessage;
            set => this.includeFormattedMessage = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not log state should be
        /// parsed into <see cref="LogRecord.StateValues"/> on generated <see
        /// cref="LogRecord"/>s. Default value: False.
        /// </summary>
        /// <remarks>
        /// Note: When <see cref="ParseStateValues"/> is set to <see
        /// langword="true"/> <see cref="LogRecord.State"/> will always be <see
        /// langword="null"/>.
        /// </remarks>
        public bool ParseStateValues
        {
            get => this.parseStateValues ?? DefaultParseStateValues;
            set => this.parseStateValues = value;
        }

        internal IServiceCollection? Services => this.services;

        /// <summary>
        /// Adds processor to the options.
        /// </summary>
        /// <remarks>
        /// Note: The supplied <paramref name="processor"/> will be
        /// automatically disposed when then the final <see
        /// cref="OpenTelemetryLoggerProvider"/> built from the options is
        /// disposed.
        /// </remarks>
        /// <param name="processor">Log processor to add.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddProcessor(BaseProcessor<LogRecord> processor)
        {
            Guard.ThrowIfNull(processor);

            this.ConfigureProvider((sp, provider) => provider.AddProcessor(processor));

            return this;
        }

        /// <summary>
        /// Adds a processor to the options which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Processor type.</typeparam>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddProcessor<T>()
            where T : BaseProcessor<LogRecord>
        {
            this.TryAddSingleton<T>();
            this.ConfigureProvider((sp, provider) => provider.AddProcessor(sp.GetRequiredService<T>()));

            return this;
        }

        /// <summary>
        /// Adds an exporter to the provider.
        /// </summary>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="exporter">LogRecord exporter to add.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddExporter(ExportProcessorType exportProcessorType, BaseExporter<LogRecord> exporter)
            => this.AddExporter(exportProcessorType, exporter, name: null, configure: null);

        /// <summary>
        /// Adds an exporter to the provider.
        /// </summary>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="exporter">LogRecord exporter to add.</param>
        /// <param name="configure"><inheritdoc cref="AddExporter{T}(ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/param[@name='configure']"/></param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddExporter(ExportProcessorType exportProcessorType, BaseExporter<LogRecord> exporter, Action<ExportLogRecordProcessorOptions> configure)
            => this.AddExporter(exportProcessorType, exporter, name: null, configure);

        /// <summary>
        /// Adds an exporter to the provider.
        /// </summary>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="exporter">LogRecord exporter to add.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure"><inheritdoc cref="AddExporter{T}(ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/param[@name='configure']"/></param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddExporter(
            ExportProcessorType exportProcessorType,
            BaseExporter<LogRecord> exporter,
            string? name,
            Action<ExportLogRecordProcessorOptions>? configure)
        {
            Guard.ThrowIfNull(exporter);

            this.ConfigureProvider((sp, provider)
                => provider.AddProcessor(
                    BuildExportProcessor(sp, exportProcessorType, exporter, name, configure)));

            return this;
        }

        /// <summary>
        /// Adds an exporter to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks><inheritdoc cref="AddExporter{T}(ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/remarks"/></remarks>
        /// <typeparam name="T">Exporter type.</typeparam>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddExporter<T>(ExportProcessorType exportProcessorType)
            where T : BaseExporter<LogRecord>
            => this.AddExporter<T>(exportProcessorType, name: null, configure: null);

        /// <summary>
        /// Adds an exporter to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks><inheritdoc cref="AddExporter{T}(ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/remarks"/></remarks>
        /// <typeparam name="T">Exporter type.</typeparam>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="configure"><inheritdoc cref="AddExporter{T}(ExportProcessorType, string?, Action{ExportLogRecordProcessorOptions}?)" path="/param[@name='configure']"/></param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddExporter<T>(ExportProcessorType exportProcessorType, Action<ExportLogRecordProcessorOptions> configure)
            where T : BaseExporter<LogRecord>
            => this.AddExporter<T>(exportProcessorType, name: null, configure);

        /// <summary>
        /// Adds an exporter to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Exporter type.</typeparam>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action to configure <see
        /// cref="ExportLogRecordProcessorOptions"/>. Only invoked when <paramref
        /// name="exportProcessorType"/> is <see
        /// cref="ExportProcessorType.Batch"/>.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddExporter<T>(
            ExportProcessorType exportProcessorType,
            string? name,
            Action<ExportLogRecordProcessorOptions>? configure)
            where T : BaseExporter<LogRecord>
        {
            this.TryAddSingleton<T>();
            this.ConfigureProvider((sp, provider)
                => provider.AddProcessor(
                    BuildExportProcessor(sp, exportProcessorType, sp.GetRequiredService<T>(), name, configure)));

            return this;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// You should usually use <see cref="ConfigureResource(Action{ResourceBuilder})"/> instead
        /// (call <see cref="ResourceBuilder.Clear"/> if desired).
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Guard.ThrowIfNull(resourceBuilder);

            return this.ConfigureProvider((sp, provider) => provider.ResourceBuilder = resourceBuilder);
        }

        /// <summary>
        /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from in-place.
        /// </summary>
        /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions ConfigureResource(
            Action<ResourceBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            this.ConfigureProvider((sp, provider) =>
            {
                Debug.Assert(provider.ResourceBuilder != null, "provider.ResourceBuilder was null");

                configure(provider.ResourceBuilder!);
            });

            return this;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="IServiceCollection"/> where logging services are configured.
        /// </summary>
        /// <remarks>
        /// Note: Logging services are only available during the application
        /// configuration phase. When using "Options" pattern via <see
        /// cref="OptionsServiceCollectionExtensions"/> or interfaces such as
        /// <see cref="IConfigureOptions{T}"/> logging services will be
        /// unavailable because "Options" are built after application services
        /// have been configured.
        /// </remarks>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions ConfigureServices(
            Action<IServiceCollection> configure)
        {
            Guard.ThrowIfNull(configure);

            var services = this.services;

            if (services == null)
            {
                throw new NotSupportedException("Services cannot be configured outside of application configuration phase.");
            }

            configure(services);

            return this;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="OpenTelemetryLoggerProvider"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions ConfigureProvider(
            Action<IServiceProvider, OpenTelemetryLoggerProvider> configure)
        {
            Guard.ThrowIfNull(configure);

            var configurationActions = this.ConfigurationActions;
            if (configurationActions == null)
            {
                throw new NotSupportedException("Configuration actions cannot be registered on options after OpenTelemetryLoggerProvider has been created.");
            }

            configurationActions.Add(configure);

            return this;
        }

        /// <summary>
        /// Sets the value of the <see cref="IncludeFormattedMessage"/> options.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to enable the option or
        /// <see langword="false"/> to disable it.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for
        /// chaining.</returns>
        public OpenTelemetryLoggerOptions SetIncludeFormattedMessage(bool enabled)
        {
            this.includeFormattedMessage = enabled;
            return this;
        }

        /// <summary>
        /// Sets the value of the <see cref="IncludeScopes"/> options.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to enable the option or
        /// <see langword="false"/> to disable it.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for
        /// chaining.</returns>
        public OpenTelemetryLoggerOptions SetIncludeScopes(bool enabled)
        {
            this.includeScopes = enabled;
            return this;
        }

        /// <summary>
        /// Sets the value of the <see cref="ParseStateValues"/> options.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to enable the option or
        /// <see langword="false"/> to disable it.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for
        /// chaining.</returns>
        public OpenTelemetryLoggerOptions SetParseStateValues(bool enabled)
        {
            this.parseStateValues = enabled;
            return this;
        }

        internal OpenTelemetryLoggerProvider Build()
        {
            var services = this.services;

            if (services == null)
            {
                throw new NotSupportedException("OpenTelemetryLoggerOptions build method cannot be called multiple times.");
            }

            this.services = null;

#if DEBUG
            bool validateScopes = true;
#else
            bool validateScopes = false;
#endif
            var serviceProvider = services.BuildServiceProvider(validateScopes);

            var finalOptions = serviceProvider.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

            this.ApplyTo(finalOptions);

            var provider = new OpenTelemetryLoggerProvider(
                finalOptions,
                serviceProvider,
                ownsServiceProvider: true);

            this.ConfigurationActions = null;

            return provider;
        }

        internal void ApplyTo(OpenTelemetryLoggerOptions other)
        {
            Debug.Assert(other != null, "other instance was null");

            if (this.includeFormattedMessage.HasValue)
            {
                other!.includeFormattedMessage = this.includeFormattedMessage;
            }

            if (this.includeScopes.HasValue)
            {
                other!.includeScopes = this.includeScopes;
            }

            if (this.parseStateValues.HasValue)
            {
                other!.parseStateValues = this.parseStateValues;
            }

            Debug.Assert(this.ConfigurationActions != null && other!.ConfigurationActions != null, "ConfigurationActions was null");

            foreach (var configurationAction in this.ConfigurationActions!)
            {
                other!.ConfigurationActions!.Add(configurationAction);
            }
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

        private void TryAddSingleton<T>()
            where T : class
        {
            var services = this.services;

            services?.TryAddSingleton<T>();
        }
    }
}
