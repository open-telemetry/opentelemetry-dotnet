using System.Diagnostics;

namespace OpenTelemetry.Exporter
{
    public interface ITraceExporterOptions
    {
        /// <summary>
        /// Gets or sets the export processor type to be use. The default value
        /// is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        ExportProcessorType ExportProcessorType { get; set; }

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless
        /// ExportProcessorType is Batch.
        /// </summary>
        BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; }
    }
}
