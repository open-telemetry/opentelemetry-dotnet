using System;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging
{
    public static class CredScanLoggingExtensions
    {
        public static OpenTelemetryLoggerOptions AddMyLogExporter(this OpenTelemetryLoggerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var credScanExporter = new CredScanExporter();
            return options.AddProcessor(new BatchLogFilteringProcessor(credScanExporter));
        }
    }
}
