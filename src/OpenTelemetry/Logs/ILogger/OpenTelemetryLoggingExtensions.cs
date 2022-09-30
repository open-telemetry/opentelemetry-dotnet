// <copyright file="OpenTelemetryLoggingExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Contains extension methods for registering <see cref="LoggerProvider"/> into a <see cref="ILoggingBuilder"/> instance.
    /// </summary>
    public static class OpenTelemetryLoggingExtensions
    {
        /// <summary>
        /// Adds an OpenTelemetry <see cref="ILoggerProvider"/> named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item>
        /// This is safe to be called multiple times. Only a single <see
        /// cref="LoggerProvider"/> will be created for a given <see
        /// cref="IServiceCollection"/> and only a single <see
        /// cref="ILoggerProvider"/> registered with the <see
        /// cref="ILoggerFactory"/>.
        /// </item>
        /// <item>
        /// This method should be called by application host code. Library
        /// authors should call <see
        /// cref="LoggerProviderBuilderServiceCollectionExtensions.ConfigureOpenTelemetryLogging(IServiceCollection)"/>
        /// instead.
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <returns><see cref="OpenTelemetryLoggingBuilder"/>.</returns>
        public static OpenTelemetryLoggingBuilder AddOpenTelemetry(this ILoggingBuilder builder)
        {
            Guard.ThrowIfNull(builder);

            builder.AddConfiguration();

            // Note: This will bind logger options element (eg "Logging:OpenTelemetry") to OpenTelemetryLoggerOptions
            LoggerProviderOptions.RegisterProviderOptions<OpenTelemetryLoggerOptions, OpenTelemetryLoggerProvider>(builder.Services);

            builder.Services.ConfigureOpenTelemetryLogging(
                loggerBuilder =>
                {
                    loggerBuilder.ConfigureBuilder((sp, loggerBuilder) =>
                    {
                        var options = sp.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;

                        if (options.ResourceBuilder != null)
                        {
                            loggerBuilder.SetResourceBuilder(options.ResourceBuilder);

                            options.ResourceBuilder = null;
                        }

                        foreach (var processor in options.Processors)
                        {
                            loggerBuilder.AddProcessor(processor);
                        }

                        options.Processors.Clear();
                    });
                });

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>(
                    sp => new OpenTelemetryLoggerProvider(
                        sp.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value,
                        sp.GetRequiredService<LoggerProvider>(),
                        disposeProvider: false)));

            return new OpenTelemetryLoggingBuilder(builder.Services);
        }

        /// <summary>
        /// Adds an OpenTelemetry <see cref="ILoggerProvider"/> named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <param name="configureOptions">Callback action to configure the <see
        /// cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <returns><see cref="OpenTelemetryLoggingBuilder"/>.</returns>
        public static OpenTelemetryLoggingBuilder AddOpenTelemetry(
            this ILoggingBuilder builder,
            Action<OpenTelemetryLoggerOptions> configureOptions)
        {
            Guard.ThrowIfNull(configureOptions);

            var loggingBuilder = AddOpenTelemetry(builder);

            loggingBuilder.Services.Configure(configureOptions);

            return loggingBuilder;
        }

        /// <summary>
        /// Adds an OpenTelemetry <see cref="ILoggerProvider"/> named 'OpenTelemetry' to the
        /// <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item>
        /// The supplied <see cref="LoggerProvider"/> will NOT be disposed when
        /// the <see cref="ILoggerFactory"/> built from <paramref
        /// name="builder"/> is disposed.
        /// </item>
        /// <item>
        /// Only a single OpenTelemetry <see cref="ILoggerProvider"/> can be registered for a
        /// given <see cref="IServiceCollection"/>. Additional calls to this
        /// method will be ignored.
        /// </item>
        /// <item>
        /// This method should be called by application host code. Library
        /// authors should call <see
        /// cref="LoggerProviderBuilderServiceCollectionExtensions.ConfigureOpenTelemetryLogging(IServiceCollection)"/>
        /// instead.
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to
        /// use.</param>
        /// <param name="loggerProvider"><see cref="LoggerProvider"/>.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call
        /// chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(
            this ILoggingBuilder builder,
            LoggerProvider loggerProvider)
            => AddOpenTelemetry(builder, loggerProvider, configureOptions: null, disposeProvider: false);

        /// <summary>
        /// Adds an OpenTelemetry <see cref="ILoggerProvider"/> named 'OpenTelemetry' to the
        /// <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder, LoggerProvider)" path="/remarks"/></remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to
        /// use.</param>
        /// <param name="loggerProvider"><see cref="LoggerProvider"/>.</param>
        /// <param name="configureOptions">Optional callback action to configure <see
        /// cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call
        /// chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(
            this ILoggingBuilder builder,
            LoggerProvider loggerProvider,
            Action<OpenTelemetryLoggerOptions> configureOptions)
            => AddOpenTelemetry(builder, loggerProvider, configureOptions, disposeProvider: false);

        /// <summary>
        /// Adds an OpenTelemetry <see cref="ILoggerProvider"/> named 'OpenTelemetry' to the
        /// <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item>
        /// Only a single OpenTelemetry <see cref="ILoggerProvider"/> can be registered for a
        /// given <see cref="IServiceCollection"/>. Additional calls to this
        /// method will be ignored.
        /// </item>
        /// <item>
        /// This method should be called by application host code. Library
        /// authors should call <see
        /// cref="LoggerProviderBuilderServiceCollectionExtensions.ConfigureOpenTelemetryLogging(IServiceCollection)"/>
        /// instead.
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to
        /// use.</param>
        /// <param name="loggerProvider"><see cref="LoggerProvider"/>.</param>
        /// <param name="configureOptions">Optional callback action to configure <see
        /// cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <param name="disposeProvider">Controls whether or not the supplied
        /// <paramref name="loggerProvider"/> will be disposed when the <see
        /// cref="ILoggerFactory"/> is disposed.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call
        /// chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(
            this ILoggingBuilder builder,
            LoggerProvider loggerProvider,
            Action<OpenTelemetryLoggerOptions>? configureOptions,
            bool disposeProvider)
        {
            Guard.ThrowIfNull(builder);
            Guard.ThrowIfNull(loggerProvider);

            // Note: Currently if multiple OpenTelemetryLoggerProvider instances
            // are added to the same ILoggingBuilder everything after the first
            // is silently ignored.

            if (configureOptions != null)
            {
                builder.Services.Configure(configureOptions);
            }

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;

                    if (options.Processors.Count > 0 || options.ResourceBuilder != null)
                    {
                        throw new NotSupportedException("Configuring processors or resource via options for an external provider is not supported.");
                    }

                    return new OpenTelemetryLoggerProvider(
                        options,
                        loggerProvider,
                        disposeProvider);
                }));

            return builder;
        }
    }
}
