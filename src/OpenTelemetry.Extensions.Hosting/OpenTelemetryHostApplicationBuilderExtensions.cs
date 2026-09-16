// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring OpenTelemetry on an application host.
/// </summary>
public static class OpenTelemetryHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds OpenTelemetry SDK services to the application host.
    /// </summary>
    /// <remarks>
    /// <para>This method is safe to call multiple times. OpenTelemetry services are
    /// started and stopped with the host.</para>
    /// <para>This method registers <see cref="IHostEnvironment.ApplicationName"/>
    /// as the default <c>service.name</c> resource attribute and
    /// <see cref="IHostEnvironment.EnvironmentName"/> as the default
    /// <c>deployment.environment.name</c> resource attribute. Both defaults are
    /// overridden by the <c>OTEL_SERVICE_NAME</c> / <c>OTEL_RESOURCE_ATTRIBUTES</c>
    /// environment variables or by any
    /// <see cref="OpenTelemetryBuilderSdkExtensions.ConfigureResource"/> call.</para>
    /// </remarks>
    /// <param name="builder">The application host builder.</param>
    /// <returns>An <see cref="OpenTelemetryBuilder"/> for configuring OpenTelemetry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static OpenTelemetryBuilder AddOpenTelemetry(this IHostApplicationBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        // Captures configuration at registration time so SDK-internal code in lower-level assemblies
        // can access it without those assemblies taking a hard dependency on IConfiguration.
        builder.Services.TryAddSingleton(new OpenTelemetryBuilderConfigurationAccessor(builder.Configuration));

        // IHostApplicationBuilder does not register IConfigurationManager in DI; registering
        // the live builder instance lets extensions that only receive IServiceCollection contribute
        // configuration sources during setup.
        builder.Services.TryAddSingleton(builder.Configuration);

        var openTelemetryBuilder = builder.Services.AddOpenTelemetry();

        if (!builder.Services.Any(
            static descriptor => descriptor.ServiceType == typeof(HostEnvironmentResourceConfigurationMarker)))
        {
            builder.Services.AddSingleton<HostEnvironmentResourceConfigurationMarker>();

            var resourceConfiguration = new HostEnvironmentResourceConfiguration(builder.Environment);

            // Resource defaults must run before any application configuration regardless of
            // registration order. Insert the callbacks at the start of the service collection,
            // while preserving registration order for all application callbacks.
            builder.Services.Insert(
                0,
                ServiceDescriptor.Singleton<IConfigureTracerProviderBuilder>(resourceConfiguration));
            builder.Services.Insert(
                0,
                ServiceDescriptor.Singleton<IConfigureMeterProviderBuilder>(resourceConfiguration));
            builder.Services.Insert(
                0,
                ServiceDescriptor.Singleton<IConfigureLoggerProviderBuilder>(resourceConfiguration));
        }

        return openTelemetryBuilder;
    }

    private sealed class HostEnvironmentResourceConfigurationMarker;

    private sealed class HostEnvironmentResourceConfiguration(IHostEnvironment environment) :
        IConfigureTracerProviderBuilder,
        IConfigureMeterProviderBuilder,
        IConfigureLoggerProviderBuilder
    {
        void IConfigureTracerProviderBuilder.ConfigureBuilder(
            IServiceProvider serviceProvider,
            TracerProviderBuilder tracerProviderBuilder)
            => tracerProviderBuilder.ConfigureResource(
                resourceBuilder => this.ConfigureResource(serviceProvider, resourceBuilder));

        void IConfigureMeterProviderBuilder.ConfigureBuilder(
            IServiceProvider serviceProvider,
            MeterProviderBuilder meterProviderBuilder)
            => meterProviderBuilder.ConfigureResource(
                resourceBuilder => this.ConfigureResource(serviceProvider, resourceBuilder));

        void IConfigureLoggerProviderBuilder.ConfigureBuilder(
            IServiceProvider serviceProvider,
            LoggerProviderBuilder loggerProviderBuilder)
            => loggerProviderBuilder.ConfigureResource(
                resourceBuilder => this.ConfigureResource(serviceProvider, resourceBuilder));

        private void ConfigureResource(
            IServiceProvider serviceProvider,
            ResourceBuilder resourceBuilder)
            => resourceBuilder.AddDetector(
                new HostEnvironmentResourceDetector(
                    environment,
                    serviceProvider.GetService<IConfiguration>()));
    }
}
