namespace OpenTelemetry.Exporter
{
    public interface ITraceExporterOptionsBuilder<TOptions, TBuilder>
    {
        TBuilder BuilderInstance { get; }

        TOptions BuilderOptions { get; }
    }
}
