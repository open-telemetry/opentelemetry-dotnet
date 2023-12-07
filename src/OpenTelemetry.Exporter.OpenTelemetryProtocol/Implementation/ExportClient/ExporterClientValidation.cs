// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal static class ExporterClientValidation
{
    internal static void EnsureUnencryptedSupportIsEnabled(OtlpExporterOptions options)
    {
        var version = Environment.Version;

        // This verification is only required for .NET Core 3.x
        if (version.Major != 3)
        {
            return;
        }

        if (options.Endpoint.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            if (AppContext.TryGetSwitch(
                    "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var unencryptedIsSupported) == false
                || unencryptedIsSupported == false)
            {
                throw new InvalidOperationException(
                    "Calling insecure gRPC services on .NET Core 3.x requires enabling the 'System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport' switch. See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client");
            }
        }
    }
}
