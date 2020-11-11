using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
