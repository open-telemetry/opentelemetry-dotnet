// <copyright file="OtlpTraceExportProcessorBuilder.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    internal class OtlpTraceExportProcessorBuilder
    {
        private readonly TracerProviderBuilder tracerProviderBuilder;
        private readonly string name;

        internal OtlpTraceExportProcessorBuilder(
            TracerProviderBuilder tracerProviderBuilder,
            string name,
            IConfiguration configuration)
        {
            this.tracerProviderBuilder = tracerProviderBuilder;
            this.name = name;

            if (configuration != null)
            {
                this.tracerProviderBuilder.ConfigureServices(services =>
                {
                    services.AddOptions<OtlpTraceExporterOptions>(name).Bind(configuration);
                    services.AddOptions<ExportActivityProcessorOptions>(name).Bind(configuration.GetSection("ActivityProcessorOptions"));
                });
            }
        }

        public OtlpTraceExportProcessorBuilder ConfigureExporterOptions(Action<OtlpTraceExporterOptions> configure)
        {
            Guard.ThrowIfNull(configure);

            return this.ConfigureServices(services => services.Configure(this.name, configure));
        }

        public OtlpTraceExportProcessorBuilder ConfigureProcessorOptions(Action<ExportActivityProcessorOptions> configure)
        {
            Guard.ThrowIfNull(configure);

            return this.ConfigureServices(services => services.Configure(this.name, configure));
        }

        public OtlpTraceExportProcessorBuilder ConfigureSdkLimitOptions(Action<SdkLimitOptions> configure)
        {
            Guard.ThrowIfNull(configure);

            // Note: We don't use this.name here, SdkLimitOptions are global.
            return this.ConfigureServices(services => services.Configure(configure));
        }

        internal OtlpTraceExportProcessorBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            this.tracerProviderBuilder.ConfigureServices(configure);

            return this;
        }

        internal BaseProcessor<Activity> BuildProcessor(IServiceProvider serviceProvider)
        {
            var exporterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpTraceExporterOptions>>().Get(this.name);
            var processorOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ExportActivityProcessorOptions>>().Get(this.name);

            // Note: Not using this.name here for SdkLimitOptions.
            // There should only be one provider for a given service
            // collection so SdkLimitOptions is treated as a single default
            // instance.
            var sdkLimitOptions = serviceProvider.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            var exporter = new OtlpTraceExporter(exporterOptions, sdkLimitOptions);

            if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                return new SimpleActivityExportProcessor(exporter);
            }
            else
            {
                return new BatchActivityExportProcessor(
                    exporter,
                    processorOptions.BatchExportProcessorOptions.MaxQueueSize,
                    processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    processorOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    processorOptions.BatchExportProcessorOptions.MaxExportBatchSize);
            }
        }
    }
}
