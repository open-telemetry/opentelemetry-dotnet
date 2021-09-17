using System;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging
{
#if ENABLE_AUDIT
    internal static class GenevaLoggingExtensions
#else
    public static class GenevaLoggingExtensions
#endif
    {
        public static OpenTelemetryLoggerOptions AddMyLogExporter(this OpenTelemetryLoggerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var exporter = new MyExporter();
            return options.AddProcessor(new BatchLogExportProcessor(exporter));
        }
    }
}
