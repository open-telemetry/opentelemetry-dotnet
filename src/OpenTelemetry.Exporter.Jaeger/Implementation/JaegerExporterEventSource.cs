using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Threading;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Exporter-Jaeger")]
    internal class JaegerExporterEventSource : EventSource
    {
        public static JaegerExporterEventSource Log = new JaegerExporterEventSource();

        [NonEvent]
        public void FailedToSend(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedToSend(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Failed to send spans: '{0}'", Level = EventLevel.Error)]
        public void FailedToSend(string exception)
        {
            this.WriteEvent(1, exception);
        }

        /// <summary>
        /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
        /// appropriate for diagnostics tracing.
        /// </summary>
        private static string ToInvariantString(Exception exception)
        {
            var originalUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                return exception.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }
    }
}
