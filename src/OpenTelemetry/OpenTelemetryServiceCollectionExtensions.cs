
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.DependencyInjection;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddOpenTelemetry(this IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services.AddOpenTelemetryTracerProviderBuilderServices();
        services.AddOpenTelemetryMeterProviderBuilderServices();

        return services;
    }
}
