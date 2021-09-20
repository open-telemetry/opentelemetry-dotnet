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

            Func<string, string> filter = str => str.ToUpper();
            
            var filteringExporter = new MyExporter("myExporter");
            return options.AddProcessor(new BatchLogFilteringProcessor(filteringExporter, filter));
        }
    }
}
