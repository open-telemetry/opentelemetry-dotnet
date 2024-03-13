// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

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
    /// <item>This method can only be called once. Subsequent calls will results
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
        => UseOtlpExporter(builder, name: null, configuration: null, configure: null);

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="protocol"><see cref="OtlpExportProtocol"/>.</param>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        OtlpExportProtocol protocol)
        => UseOtlpExporterWithProtocolAndBaseEndpoint(builder, protocol, baseEndpoint: null);

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="protocol"><see cref="OtlpExportProtocol"/>.</param>
    /// <param name="baseEndpoint">
    /// <para>Base endpoint to use.</para>
    /// Note: A signal-specific path will be appended to the base endpoint for
    /// each signal automatically if the protocol is set to <see
    /// cref="OtlpExportProtocol.HttpProtobuf"/>.
    /// </param>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        OtlpExportProtocol protocol,
        Uri baseEndpoint)
    {
        Guard.ThrowIfNull(baseEndpoint);

        return UseOtlpExporterWithProtocolAndBaseEndpoint(builder, protocol, baseEndpoint);
    }

    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Action<OtlpExporterBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return UseOtlpExporter(builder, name: null, configuration: null, configure);
    }

    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        return UseOtlpExporter(builder, name: null, configuration, configure: null);
    }

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

    private static IOpenTelemetryBuilder UseOtlpExporterWithProtocolAndBaseEndpoint(
        this IOpenTelemetryBuilder builder,
        OtlpExportProtocol protocol,
        Uri? baseEndpoint)
    {
        return UseOtlpExporter(builder, name: null, configuration: null, configure: otlpBuilder =>
        {
            otlpBuilder.ConfigureDefaultExporterOptions(o =>
            {
                o.Protocol = protocol;
                if (baseEndpoint != null)
                {
                    o.Endpoint = baseEndpoint;
                }
            });
        });
    }
}
