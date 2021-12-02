namespace OpenTelemetry.Exporter
{
    public interface IHttpClientFactoryExporterOptionsBuilder<TOptions, TBuilder>
    {
        TBuilder BuilderInstance { get; }
    }
}
