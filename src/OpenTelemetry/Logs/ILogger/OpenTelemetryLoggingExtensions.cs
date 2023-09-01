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

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
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
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="OpenTelemetryLoggerProvider"/> will be created
    /// for a given <see cref="IServiceCollection"/>.</item>
    /// <item><see cref="IServiceCollection"/> / <see cref="IServiceProvider"/>
    /// features (DI, Options, IConfiguration, etc.) are not available when
    /// using <see cref="ILoggingBuilder"/>.</item>
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
        RegisterLoggerProviderOptions(builder.Services);

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

        // The warning here is about the fact that the OpenTelemetryLoggerOptions will be bound to configuration using ConfigurationBinder
        // That uses reflection a lot - so if any of the properties on that class were complex types reflection would be used on them
        // and nothing could guarantee its correctness.
        // Since currently this class only contains primitive properties this is OK. The top level properties are kept
        // because the first generic argument of RegisterProviderOptions below is annotated with
        // DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All) so it will preserve everything on the OpenTelemetryLoggerOptions.
        // But it would not work recursively into complex property values;
        // This should be fully fixed with the introduction of Configuration binder source generator in .NET 8
        // and then there should be a way to do this without any warnings.
        // The correctness of these suppressions is verified by a test which validates that all properties of OpenTelemetryLoggerOptions
        // are of a primitive type.
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "OpenTelemetryLoggerOptions contains only primitive properties.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "OpenTelemetryLoggerOptions contains only primitive properties.")]
#endif
        static void RegisterLoggerProviderOptions(IServiceCollection services)
        {
            LoggerProviderOptions.RegisterProviderOptions<OpenTelemetryLoggerOptions, OpenTelemetryLoggerProvider>(services);
        }
    }

    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
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
}
