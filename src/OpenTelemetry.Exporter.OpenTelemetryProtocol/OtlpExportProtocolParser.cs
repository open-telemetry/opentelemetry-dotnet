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
                result = OtlpExportProtocol.Grpc;
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
