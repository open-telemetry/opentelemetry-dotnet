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
    /// Adds an <see cref="ILoggerProvider"/> named 'OpenTelemetry' to the
    /// factory which will emit log messages into a <see
    /// cref="LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="OpenTelemetryLoggerProvider"/> will be created
    /// for a given <see cref="IServiceCollection"/>.</item>
    /// <item>To configure the <see cref="LoggerProvider"/> used by the <see
    /// cref="OpenTelemetryLoggerProvider"/> call <see
    /// cref="ConfigureOpenTelemetry"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    public static ILoggingBuilder AddOpenTelemetry(
        this ILoggingBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        builder.AddConfiguration();

        // Note: This will bind logger options element (eg "Logging:OpenTelemetry") to OpenTelemetryLoggerOptions
        LoggerProviderOptions.RegisterProviderOptions<OpenTelemetryLoggerOptions, OpenTelemetryLoggerProvider>(builder.Services);

        new LoggerProviderBuilderBase(builder.Services).ConfigureBuilder(
            (sp, logging) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

                if (options.ResourceBuilder != null)
                {
                    logging.SetResourceBuilder(options.ResourceBuilder);

                    options.ResourceBuilder = null;
                }

                foreach (var processor in options.Processors)
                {
                    logging.AddProcessor(processor);
                }

                options.Processors.Clear();
            });

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>(
                sp => new OpenTelemetryLoggerProvider(
                    sp.GetRequiredService<LoggerProvider>(),
                    sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue,
                    disposeProvider: false)));

        return builder;
    }

    /// <summary>
    /// <inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)"/>
    /// </summary>
    /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    public static ILoggingBuilder AddOpenTelemetry(
        this ILoggingBuilder builder,
        Action<OpenTelemetryLoggerOptions>? configure)
    {
        if (configure != null)
        {
            builder.Services.Configure(configure);
        }

        return AddOpenTelemetry(builder);
    }

    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="LoggerProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="LoggerProvider"/> will NOT be created automatically
    /// using this method. To begin collecting logs call
    /// <see cref="AddOpenTelemetry(ILoggingBuilder)"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    public static ILoggingBuilder ConfigureOpenTelemetry(
        this ILoggingBuilder builder,
        Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(builder);

        builder.Services.ConfigureOpenTelemetryLoggerProvider(configure);

        return builder;
    }
}
