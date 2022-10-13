// <copyright file="ZipkinExporterOptions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Zipkin span exporter options.
    /// OTEL_EXPORTER_ZIPKIN_ENDPOINT
    /// environment variables are parsed during object construction.
    /// </summary>
    /// <remarks>
    /// The constructor throws <see cref="FormatException"/> if it fails to parse
    /// any of the supported environment variables.
    /// </remarks>
    public sealed class ZipkinExporterOptions
    {
        internal const int DefaultMaxPayloadSizeInBytes = 4096;
        internal const string ZipkinEndpointEnvVar = "OTEL_EXPORTER_ZIPKIN_ENDPOINT";
        internal const string DefaultZipkinEndpoint = "http://localhost:9411/api/v2/spans";

        internal static readonly Func<HttpClient> DefaultHttpClientFactory = () => new HttpClient();

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipkinExporterOptions"/> class.
        /// Initializes zipkin endpoint.
        /// </summary>
        public ZipkinExporterOptions()
             : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
        {
        }

        internal ZipkinExporterOptions(IConfiguration configuration)
        {
            if (configuration.TryGetUriValue(ZipkinEndpointEnvVar, out var endpoint))
            {
                this.Endpoint = endpoint;
            }
        }

        /// <summary>
        /// Gets or sets Zipkin endpoint address. See https://zipkin.io/zipkin-api/#/default/post_spans.
        /// Typically https://zipkin-server-name:9411/api/v2/spans.
        /// </summary>
        public Uri Endpoint { get; set; } = new Uri(DefaultZipkinEndpoint);

        /// <summary>
        /// Gets or sets a value indicating whether short trace id should be used.
        /// </summary>
        public bool UseShortTraceIds { get; set; }

        /// <summary>
        /// Gets or sets the maximum payload size in bytes. Default value: 4096.
        /// </summary>
        public int? MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;

        /// <summary>
        /// Gets or sets the export processor type to be used with Zipkin Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is BatchExporter.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportActivityProcessorOptions();

        /// <summary>
        /// Gets or sets the factory function called to create the <see
        /// cref="HttpClient"/> instance that will be used at runtime to
        /// transmit spans over HTTP. The returned instance will be reused for
        /// all export invocations.
        /// </summary>
        /// <remarks>
        /// Note: The default behavior when using the <see
        /// cref="ZipkinExporterHelperExtensions.AddZipkinExporter(TracerProviderBuilder,
        /// Action{ZipkinExporterOptions})"/> extension is if an <a
        /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
        /// instance can be resolved through the application <see
        /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
        /// created through the factory with the name "ZipkinExporter" otherwise
        /// an <see cref="HttpClient"/> will be instantiated directly.
        /// </remarks>
        public Func<HttpClient> HttpClientFactory { get; set; } = DefaultHttpClientFactory;
    }
}
