// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

internal static class OtlpExportProtocolParser
{
    public static bool TryParse(string value, out OtlpExportProtocol result)
    {
        switch (value?.Trim())
        {
            case "grpc":
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
                result = OtlpExportProtocol.Grpc;
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
                return true;
            case "http/protobuf":
                result = OtlpExportProtocol.HttpProtobuf;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
