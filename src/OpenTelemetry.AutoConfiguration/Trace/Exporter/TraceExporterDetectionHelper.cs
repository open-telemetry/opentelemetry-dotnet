using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class TraceExporterDetectionHelper
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.RegisterOptionsFactory(config => new TraceExporterConfigurationOptions(config));
    }

    public static void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder, string name)
    {
        var exporterConfigurationOptions = serviceProvider.GetRequiredService<IOptionsMonitor<TraceExporterConfigurationOptions>>().Get(name ?? Options.DefaultName);

        if (!string.IsNullOrWhiteSpace(exporterConfigurationOptions.TraceExporterName)
            && !string.Equals("None", exporterConfigurationOptions.TraceExporterName, StringComparison.OrdinalIgnoreCase))
        {
            bool exporterFound = false;

            var exporterDetectors = serviceProvider.GetServices<ITraceExporterDetector>();
            foreach (var exporterDetector in exporterDetectors)
            {
                if (string.Equals(exporterConfigurationOptions.TraceExporterName, exporterDetector.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var exporter = exporterDetector.Create(serviceProvider);
                    if (exporter != null)
                    {
                        tracerProviderBuilder.AddProcessor(exporter);
                        exporterFound = true;
                    }

                    break;
                }
            }

            if (!exporterFound)
            {
                // TBD: Not sure if this should be a throw or a log.
                throw new InvalidOperationException($"TraceExporterDetector for name '{exporterConfigurationOptions.TraceExporterName}' could not be found or did not return a valid exporter.");
            }
        }
    }
}
