namespace OpenTelemetry.Exporter.ElasticApm
{
    public sealed class IntakeApiVersion
    {
        public static readonly IntakeApiVersion V2 = new IntakeApiVersion("/intake/v2/events");

        private readonly string endpoint;

        private IntakeApiVersion(string endpoint)
        {
            this.endpoint = endpoint;
        }

        public static implicit operator string(IntakeApiVersion value) => value.endpoint;
    }
}
