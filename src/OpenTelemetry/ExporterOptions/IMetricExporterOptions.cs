using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter
{
    public interface IMetricExporterOptions
    {
        /// <summary>
        /// Gets or sets the <see cref="MetricReaderType" /> to use. Defaults to
        /// <c>MetricReaderType.Periodic</c>.
        /// </summary>
        MetricReaderType MetricReaderType { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PeriodicExportingMetricReaderOptions" />
        /// options. Ignored unless <c>MetricReaderType</c> is <c>Periodic</c>.
        /// </summary>
        PeriodicExportingMetricReaderOptions PeriodicExportingMetricReaderOptions { get; set; }

        /// <summary>
        /// Gets or sets the AggregationTemporality used for Histogram and Sum
        /// metrics.
        /// </summary>
        AggregationTemporality AggregationTemporality { get; set; }
    }
}
