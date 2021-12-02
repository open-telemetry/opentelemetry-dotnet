namespace OpenTelemetry.Exporter
{
    public interface IMetricExporterOptionsBuilder<TOptions, TBuilder>
    {
        TBuilder BuilderInstance { get; }

        TOptions BuilderOptions { get; }
    }
}
