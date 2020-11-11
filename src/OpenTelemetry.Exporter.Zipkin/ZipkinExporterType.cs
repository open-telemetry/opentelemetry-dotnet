namespace OpenTelemetry.Exporter.Zipkin
{
    /// <summary>
    /// Type of exporter to be used.
    /// </summary>
    public enum ZipkinExporterType
    {
        /// <summary>
        /// Use SimpleExportProcessor
        /// </summary>
        SimpleExportProcessor,

        /// <summary>
        /// Use BatchExportProcessor
        /// </summary>
        BatchExportProcessor,
    }
}
