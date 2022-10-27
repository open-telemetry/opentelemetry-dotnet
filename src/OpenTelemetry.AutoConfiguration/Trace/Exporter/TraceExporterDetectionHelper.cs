// <copyright file="TraceExporterDetectionHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

    public static void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder, string? name)
    {
        name ??= Options.DefaultName;

        var exporterConfigurationOptions = serviceProvider.GetRequiredService<IOptionsMonitor<TraceExporterConfigurationOptions>>().Get(name);

        if (!string.IsNullOrWhiteSpace(exporterConfigurationOptions.TraceExporterName)
            && !string.Equals("None", exporterConfigurationOptions.TraceExporterName, StringComparison.OrdinalIgnoreCase))
        {
            bool exporterFound = false;

            var exporterDetectors = serviceProvider.GetServices<ITraceExporterDetector>();
            foreach (var exporterDetector in exporterDetectors)
            {
                if (string.Equals(exporterConfigurationOptions.TraceExporterName, exporterDetector.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var processor = exporterDetector.Create(serviceProvider, name);
                    if (processor != null)
                    {
                        tracerProviderBuilder.AddProcessor(processor);
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
