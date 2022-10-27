using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class TraceSamplerDetectionHelper
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.RegisterOptionsFactory(config => new TraceSamplerConfigurationOptions(config));

        services.TryAddSingleton<ITraceSamplerDetector, TraceAlwaysOnSamplerDetector>();
        services.TryAddSingleton<ITraceSamplerDetector, TraceAlwaysOffSamplerDetector>();
        services.TryAddSingleton<ITraceSamplerDetector, TraceIdRatioBasedSamplerDetector>();
        services.TryAddSingleton<ITraceSamplerDetector, TraceParentBasedAlwaysOnSamplerDetector>();
        services.TryAddSingleton<ITraceSamplerDetector, TraceParentBasedAlwaysOffSamplerDetector>();
        services.TryAddSingleton<ITraceSamplerDetector, TraceParentBasedIdRatioSamplerDetector>();
    }

    public static void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder, string name)
    {
        var samplerConfigurationOptions = serviceProvider.GetRequiredService<IOptionsMonitor<TraceSamplerConfigurationOptions>>().Get(name ?? Options.DefaultName);

        if (!string.IsNullOrWhiteSpace(samplerConfigurationOptions.TraceSamplerName))
        {
            bool samplerFound = false;

            var samplerDetectors = serviceProvider.GetServices<ITraceSamplerDetector>();
            foreach (var samplerDetector in samplerDetectors)
            {
                if (string.Equals(samplerConfigurationOptions.TraceSamplerName, samplerDetector.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var sampler = samplerDetector.Create(serviceProvider, samplerConfigurationOptions.TraceSamplerArgument);
                    if (sampler != null)
                    {
                        tracerProviderBuilder.SetSampler(sampler);
                        samplerFound = true;
                    }

                    break;
                }
            }

            if (!samplerFound)
            {
                // TBD: Not sure if this should be a throw or a log.
                throw new InvalidOperationException($"TraceSamplerDetector for name '{samplerConfigurationOptions.TraceSamplerName}' could not be found or did not return a valid sampler.");
            }
        }
    }
}
