using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

public static class OpenTelemetrySdkTracingAutoConfigurationExtensions
{
    public static TracerProviderBuilder AddAutoConfiguration(this TracerProviderBuilder tracerProviderBuilder, string name)
    {
        Guard.ThrowIfNull(tracerProviderBuilder);

        tracerProviderBuilder.ConfigureServices(services =>
        {
            TraceSamplerDetectionHelper.ConfigureServices(services);
        });

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            TraceSamplerDetectionHelper.ConfigureBuilder(sp, builder, name);
        });

        return tracerProviderBuilder;
    }
}
