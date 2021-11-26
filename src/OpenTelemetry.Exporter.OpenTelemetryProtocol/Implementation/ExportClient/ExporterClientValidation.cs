using System;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    internal static class ExporterClientValidation
    {
        internal static void EnsureUnencryptedSupportIsEnabled(OtlpExporterOptions options)
        {
            if (options.Endpoint.Scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase))
            {
                if (AppContext.TryGetSwitch(
                        "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var unencryptedIsSupported) == false
                    || unencryptedIsSupported == false)
                {
                    throw new InvalidOperationException(
                        "'System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport' must be enabled for using HTTP with .NET 3.1");
                }
            }
        }
    }
}
