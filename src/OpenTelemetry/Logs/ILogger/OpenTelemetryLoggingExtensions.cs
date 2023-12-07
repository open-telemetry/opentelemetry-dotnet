// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
#if EXPOSE_EXPERIMENTAL_FEATURES
using System.ComponentModel;
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
    /// <item><see cref="IServiceCollection"/> features available to metrics and
    /// traces (for example the "ConfigureServices" extension) are NOT available
    /// when using <see cref="AddOpenTelemetry(ILoggingBuilder)"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
#if EXPOSE_EXPERIMENTAL_FEATURES
    // todo: [Obsolete("Call UseOpenTelemetry instead this method will be removed in a future version.")]
    // Note: We hide AddOpenTelemetry from IDEs using EditorBrowsable when UseOpenTelemetry is present to reduce confusion.
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public static ILoggingBuilder AddOpenTelemetry(
        this ILoggingBuilder builder)
        => AddOpenTelemetryInternal(builder, configureBuilder: null, configureOptions: null);

    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
#if EXPOSE_EXPERIMENTAL_FEATURES
    // todo: [Obsolete("Call UseOpenTelemetry instead this method will be removed in a future version.")]
    // Note: We hide AddOpenTelemetry from IDEs using EditorBrowsable when UseOpenTelemetry is present to reduce confusion.
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public static ILoggingBuilder AddOpenTelemetry(
        this ILoggingBuilder builder,
        Action<OpenTelemetryLoggerOptions>? configure)
        => AddOpenTelemetryInternal(builder, configureBuilder: null, configureOptions: configure);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</para>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="OpenTelemetryLoggerProvider"/> will be created
    /// for a given <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    public
#else
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="OpenTelemetryLoggerProvider"/> will be created
    /// for a given <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    internal
#endif
        static ILoggingBuilder UseOpenTelemetry(
        this ILoggingBuilder builder)
        => AddOpenTelemetryInternal(builder, configureBuilder: null, configureOptions: null);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="UseOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure">Optional <see cref="LoggerProviderBuilder"/> configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    public
#else
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="UseOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/> configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    internal
#endif
        static ILoggingBuilder UseOpenTelemetry(
        this ILoggingBuilder builder,
        Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return AddOpenTelemetryInternal(builder, configureBuilder: configure, configureOptions: null);
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="UseOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configureBuilder">Optional <see cref="LoggerProviderBuilder"/> configuration action.</param>
    /// <param name="configureOptions">Optional <see cref="OpenTelemetryLoggerOptions"/> configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    public
#else
    /// <summary>
    /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="UseOpenTelemetry(ILoggingBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <param name="configureBuilder">Optional <see cref="LoggerProviderBuilder"/> configuration action.</param>
    /// <param name="configureOptions">Optional <see cref="OpenTelemetryLoggerOptions"/> configuration action.</param>
    /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
    internal
#endif
        static ILoggingBuilder UseOpenTelemetry(
        this ILoggingBuilder builder,
        Action<LoggerProviderBuilder>? configureBuilder,
        Action<OpenTelemetryLoggerOptions>? configureOptions)
        => AddOpenTelemetryInternal(builder, configureBuilder, configureOptions);

    private static ILoggingBuilder AddOpenTelemetryInternal(
        ILoggingBuilder builder,
        Action<LoggerProviderBuilder>? configureBuilder,
        Action<OpenTelemetryLoggerOptions>? configureOptions)
    {
        Guard.ThrowIfNull(builder);

        builder.AddConfiguration();

        var services = builder.Services;

        // Note: This will bind logger options element (eg "Logging:OpenTelemetry") to OpenTelemetryLoggerOptions
        RegisterLoggerProviderOptions(services);

        services.AddOpenTelemetrySharedProviderBuilderServices();

        if (configureOptions != null)
        {
            // Note: Order is important here so that user-supplied delegate
            // fires AFTER the options are bound to Logging:OpenTelemetry
            // configuration.
            services.Configure(configureOptions);
        }

        var loggingBuilder = new LoggerProviderBuilderBase(services).ConfigureBuilder(
            (sp, logging) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

                if (options.ResourceBuilder != null)
                {
                    logging.SetResourceBuilder(options.ResourceBuilder);

                    options.ResourceBuilder = null;
                }

                foreach (var processorFactory in options.ProcessorFactories)
                {
                    logging.AddProcessor(processorFactory);
                }

                options.ProcessorFactories.Clear();
            });

        configureBuilder?.Invoke(loggingBuilder);

        services.TryAddEnumerable(
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
}
