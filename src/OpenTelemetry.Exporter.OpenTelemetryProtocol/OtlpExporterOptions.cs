// <copyright file="OtlpExporterOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// OpenTelemetry Protocol (OTLP) exporter options.
    /// OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, OTEL_EXPORTER_OTLP_TIMEOUT, OTEL_EXPORTER_OTLP_PROTOCOL
    /// environment variables are parsed during object construction.
    /// </summary>
    public class OtlpExporterOptions : BaseOtlpExporterOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
        /// </summary>
        public OtlpExporterOptions()
            : this(new ConfigurationBuilder().AddEnvironmentVariables().Build(), new())
        {
        }

        internal OtlpExporterOptions(
            IConfiguration configuration,
            BatchExportActivityProcessorOptions defaultBatchOptions)
            : base(configuration)
        {
            Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");

            this.BatchExportProcessorOptions = defaultBatchOptions;
        }

        /// <summary>
        /// Gets or sets the export processor type to be used with the OpenTelemetry Protocol Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is Batch.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; }

        internal static void RegisterOtlpExporterOptionsFactory(IServiceCollection services)
        {
            services.RegisterOptionsFactory(
                (sp, configuration, name) => new OtlpExporterOptions(
                    configuration,
                    sp.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name)));
        }
    }
}
