// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Contains methods for configuring the OpenTelemetry SDK inside an <see
/// cref="IServiceCollection"/>.
/// </summary>
// [Obsolete] <- This class is not marked obsolete because there would be a big
// impact to do that but it technically is obsolete. If we do a major release
// with breaking changes this class can be deleted. IOpenTelemetryBuilder has
// source-compatible extensions via the SDK. We maintain this for binary
// compatibility only. Something like this might help:
// https://github.com/dotnet/csharplang/issues/7706
public sealed class OpenTelemetryBuilder : IOpenTelemetryBuilder
{
    internal OpenTelemetryBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services.AddOpenTelemetrySharedProviderBuilderServices();

        this.Services = services;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers an action to configure the <see cref="ResourceBuilder"/>s used
    /// by tracing, metrics, and logging.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied sequentially.
    /// </remarks>
    /// <param name="configure"><see cref="ResourceBuilder"/> configuration
    /// action.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder ConfigureResource(
        Action<ResourceBuilder> configure)
    {
        OpenTelemetryBuilderSdkExtensions.ConfigureResource(this, configure);
        return this;
    }

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="MeterProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.</item>
    /// <item>This method automatically registers an <see
    /// cref="IMetricsListener"/> named 'OpenTelemetry' into the <see
    /// cref="IServiceCollection"/>.</item>
    /// </list>
    /// </remarks>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithMetrics()
        => this.WithMetrics(b => { });

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithMetrics()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="MeterProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithMetrics(Action<MeterProviderBuilder> configure)
    {
        OpenTelemetryBuilderSdkExtensions.WithMetrics(this, configure);
        return this;
    }

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithTracing()
        => this.WithTracing(b => { });

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithTracing()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="TracerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithTracing(Action<TracerProviderBuilder> configure)
    {
        OpenTelemetryBuilderSdkExtensions.WithTracing(this, configure);
        return this;
    }
}
