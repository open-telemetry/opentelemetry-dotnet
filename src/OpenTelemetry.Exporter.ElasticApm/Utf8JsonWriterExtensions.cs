using System.IO;
using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm
{
    internal static class Utf8JsonWriterExtensions
    {
        private static readonly byte NewLine = (byte)'\n';

        internal static void WriteNewLine(this Utf8JsonWriter writer, Stream stream)
        {
            writer.Flush();
            writer.Reset();
            stream.WriteByte(NewLine);
        }
    }
}
