// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of gRPC client
/// instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables gRPC client instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddGrpcClientInstrumentation(this TracerProviderBuilder builder)
        => AddGrpcClientInstrumentation(builder, name: null, configure: null);

    /// <summary>
    /// Enables gRPC client instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback action for configuring <see cref="GrpcClientInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddGrpcClientInstrumentation(
        this TracerProviderBuilder builder,
        Action<GrpcClientInstrumentationOptions> configure)
        => AddGrpcClientInstrumentation(builder, name: null, configure);

    /// <summary>
    /// Enables gRPC client instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="GrpcClientInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddGrpcClientInstrumentation(
        this TracerProviderBuilder builder,
        string name,
        Action<GrpcClientInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (configure != null)
            {
                services.Configure(name, configure);
            }

            services.RegisterOptionsFactory(configuration => new GrpcClientInstrumentationOptions(configuration));
        });

        builder.AddSource(GrpcClientDiagnosticListener.ActivitySourceName);
        builder.AddLegacySource("Grpc.Net.Client.GrpcOut");

        return builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<GrpcClientInstrumentationOptions>>().Get(name);

            return new GrpcClientInstrumentation(options);
        });
    }
}