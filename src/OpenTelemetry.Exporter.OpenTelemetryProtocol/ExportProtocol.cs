namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Supported by OTLP exporter protocol types according to the specification https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
    /// </summary>
    public static class ExportProtocol
    {
        /// <summary>
        /// OTLP over gRPC .
        /// </summary>
        public const string Grpc = "grpc";

        /// <summary>
        /// OTLP over HTTP with protobuf payloads.
        /// </summary>
        public const string HttpProtobuf = "http/protobuf";
    }
}
