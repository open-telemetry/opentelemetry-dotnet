// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>
/// Reports that a response was discarded because it exceeded <see
/// cref="OtlpExporterOptions.MaxResponseSizeBytes"/>.
/// </summary>
/// <remarks>
/// The OpenTelemetry specification requires such a response to be treated as a
/// not-retryable error. <see cref="OtlpRetry"/> keys off this type so that the
/// behaviour does not depend on how a particular runtime reports the failure.
/// </remarks>
#pragma warning disable CA1064 // Exceptions should be public - an internal signal between the export client and the retry policy, never surfaced to callers.
internal sealed class ResponseSizeLimitExceededException : Exception
#pragma warning restore CA1064
{
    public ResponseSizeLimitExceededException(long? responseSizeBytes, int maxResponseSizeBytes)
        : base(FormatMessage(responseSizeBytes, maxResponseSizeBytes))
    {
    }

    public ResponseSizeLimitExceededException()
    {
    }

    public ResponseSizeLimitExceededException(string message)
        : base(message)
    {
    }

    public ResponseSizeLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private static string FormatMessage(long? responseSizeBytes, int maxResponseSizeBytes)
    {
        var size = responseSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? "an unknown number of";
        return FormattableString.Invariant($"The response size of {size} bytes exceeds the maximum response size of {maxResponseSizeBytes} bytes.");
    }
}
