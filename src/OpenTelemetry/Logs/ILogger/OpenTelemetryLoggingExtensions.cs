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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Contains extension methods for registering <see cref="OpenTelemetryLoggerProvider"/> into a <see cref="ILoggingBuilder"/> instance.
/// </summary>
public static class OpenTelemetryLoggingExtensions
{
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library
    /// authors. Only a single <see cref="OpenTelemetryLoggerProvider"/>
    /// will be created for a given <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    // todo: [Obsolete("Call AddOpenTelemetryLogging instead the AddOpenTelemetry method will be removed in a future version.")]
    public static ILoggingBuilder AddOpenTelemetry(
        this ILoggingBuilder builder)
        => AddOpenTelemetryLogging(builder);

    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    // todo: [Obsolete("Call AddOpenTelemetryLogging and use LoggerProviderBuilder.ConfigureLoggerOptions instead. The AddOpenTelemetry method will be removed in a future version.")]
    public static ILoggingBuilder AddOpenTelemetry(
        this ILoggingBuilder builder,
        Action<OpenTelemetryLoggerOptions>? configure)
    {
        AddOpenTelemetryLogging(builder);

        if (configure != null)
        {
            builder.Services.Configure(configure);
        }

        return builder;
    }

    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    internal static ILoggingBuilder AddOpenTelemetryLogging(
        this ILoggingBuilder builder)
        => AddOpenTelemetryLogging(builder, b => { });

    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    internal static ILoggingBuilder AddOpenTelemetryLogging(
        this ILoggingBuilder builder,
        Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        AddOpenTelemetryLoggerIntegration(builder);

        var loggerBuilder = new LoggerProviderServiceCollectionBuilder(builder.Services);

        // Note: This code is to support legacy AddProcessor & SetResourceBuilder APIs on OpenTelemetryLoggerOptions.
        loggerBuilder.ConfigureBuilder((sp, sdkLoggerBuilder) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

            if (options.ResourceBuilder != null)
            {
                sdkLoggerBuilder.SetResourceBuilder(options.ResourceBuilder);

                options.ResourceBuilder = null;
            }

            foreach (var processor in options.Processors)
            {
                sdkLoggerBuilder.AddProcessor(processor);
            }

            options.Processors.Clear();
        });

        configure(loggerBuilder);

        return builder;
    }

    private static void AddOpenTelemetryLoggerIntegration(ILoggingBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        builder.AddConfiguration();

        // Note: This will bind logger options element (eg "Logging:OpenTelemetry") to OpenTelemetryLoggerOptions
        LoggerProviderOptions.RegisterProviderOptions<OpenTelemetryLoggerOptions, OpenTelemetryLoggerProvider>(builder.Services);

        builder.Services.AddOpenTelemetrySharedProviderBuilderServices();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>(
                sp => new OpenTelemetryLoggerProvider(
                    sp.GetRequiredService<LoggerProvider>(),
                    sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue,
                    disposeProvider: false)));
    }
}
