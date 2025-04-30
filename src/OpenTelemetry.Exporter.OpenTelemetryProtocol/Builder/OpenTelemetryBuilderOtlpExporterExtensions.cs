// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Contains extension methods to facilitate registration of the OpenTelemetry
/// Protocol (OTLP) exporter into an <see cref="IOpenTelemetryBuilder"/>
/// instance.
/// </summary>
public static class OpenTelemetryBuilderOtlpExporterExtensions
{
    /// <summary>
    /// Uses OpenTelemetry Protocol (OTLP) exporter for all signals.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>Calling this method automatically enables logging, metrics, and
    /// tracing.</item>
    /// <item>The exporter registered by this method will be added as the last
    /// processor in the pipeline established for logging and tracing.</item>
    /// <item>This method can only be called once. Subsequent calls will result
    /// in a <see cref="NotSupportedException"/> being thrown.</item>
    /// <item>This method cannot be called in addition to signal-specific
    /// <c>AddOtlpExporter</c> methods. If this method is called signal-specific
    /// <c>AddOtlpExporter</c> calls will result in a <see
    /// cref="NotSupportedException"/> being thrown.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <returns>Supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.</returns>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder)
#pragma warning disable CA1062 // Validate arguments of public methods
        => UseOtlpExporter(builder, name: null, configuration: null, configure: null);
#pragma warning restore CA1062 // Validate arguments of public methods

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="protocol"><see cref="OtlpExportProtocol"/>.</param>
    /// <param name="baseUrl">
    /// <para>Base URL to use.</para>
    /// Note: A signal-specific path will be appended to the base URL for each
    /// signal automatically if the protocol is set to <see
    /// cref="OtlpExportProtocol.HttpProtobuf"/>.
    /// </param>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        OtlpExportProtocol protocol,
        Uri baseUrl)
    {
        Guard.ThrowIfNull(baseUrl);

#pragma warning disable CA1062 // Validate arguments of public methods
        return UseOtlpExporter(builder, name: null, configuration: null, configure: otlpBuilder =>
#pragma warning restore CA1062 // Validate arguments of public methods
        {
            otlpBuilder.ConfigureDefaultExporterOptions(o =>
            {
                o.Protocol = protocol;
                o.Endpoint = baseUrl;
            });
        });
    }

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterBuilder"/>.</param>
    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Action<OtlpExporterBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return UseOtlpExporter(builder, name: null, configuration: null, configure);
    }

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configuration">
    /// <para><see cref="IConfiguration"/> to bind onto <see cref="OtlpExporterBuilderOptions"/>.</para>
    /// <para>Notes:
    /// <list type="bullet">
    /// <item docLink="true"><see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md"/>
    /// for details on the configuration schema.</item>
    /// <item>The <see cref="OtlpExporterBuilderOptions"/> instance will be
    /// named "otlp" by default when calling this method.</item>
    /// </list>
    /// </para>
    /// </param>
    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        return UseOtlpExporter(builder, name: null, configuration, configure: null);
    }

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configuration">
    /// <para>Optional <see cref="IConfiguration"/> to bind onto <see
    /// cref="OtlpExporterBuilderOptions"/>.</para>
    /// <para>Notes:
    /// <list type="bullet">
    /// <item><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder,
    /// IConfiguration)"
    /// path="/param[@name='configuration']/para/list/item[@docLink='true']"/></item>
    /// <item>If <paramref name="name"/> is not set the <see
    /// cref="OtlpExporterBuilderOptions"/> instance will be named "otlp" by
    /// default when <paramref name="configuration"/> is used.</item>
    /// </list>
    /// </para>
    /// </param>
    /// <param name="configure">Optional callback action for configuring <see cref="OtlpExporterBuilder"/>.</param>
    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        string? name,
        IConfiguration? configuration,
        Action<OtlpExporterBuilder>? configure)
    {
        Guard.ThrowIfNull(builder);

        // Note: We automatically turn on signals for "UseOtlpExporter"
        builder
            .WithLogging()
            .WithMetrics()
            .WithTracing();

        var otlpBuilder = new OtlpExporterBuilder(builder.Services, name, configuration);

        configure?.Invoke(otlpBuilder);

        return builder;
    }
}
