using System.Diagnostics;

namespace OpenTelemetry.Exporter.ElasticApm
{
    public class ElasticApmOptions
    {
        internal const int DefaultMaxPayloadSizeInBytes = 4096;

        /// <summary>
        /// Gets or sets Elastic APM Server host. Default value: localhost.
        /// https://www.elastic.co/guide/en/apm/server/current/configuration-process.html#host.
        /// </summary>
        public string ServerHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets Elastic APM Server port. Default value: 8200.
        /// https://www.elastic.co/guide/en/apm/server/current/configuration-process.html#host.
        /// </summary>
        public int ServerPort { get; set; } = 8200;

        /// <summary>
        /// Gets or sets Elastic APM Server scheme. Default value: http.
        /// </summary>
        public string ServerScheme { get; set; } = "http";

        /// <summary>
        /// Gets or sets application environment. Default value: Dev.
        /// </summary>
        public string Environment { get; set; } = "Dev";

        /// <summary>
        /// Gets or sets application name. Default value: MyService.
        /// </summary>
        public string Name { get; set; } = "MyService";

        /// <summary>
        /// Gets or sets the maximum payload size in bytes. Default value: 4096.
        /// </summary>
        public int? MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;

        /// <summary>
        /// Gets or sets Elastic APM Server API version. Default value: IntakeApiVersion.V2.
        /// https://www.elastic.co/guide/en/apm/server/current/events-api.html#events-api-endpoint.
        /// </summary>
        public IntakeApiVersion IntakeApiVersion { get; set; } = IntakeApiVersion.V2;

        /// <summary>
        /// Gets or sets the export processor type to be used with Elastic APM Exporter.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is BatchExporter.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportProcessorOptions<Activity>();
    }
}
