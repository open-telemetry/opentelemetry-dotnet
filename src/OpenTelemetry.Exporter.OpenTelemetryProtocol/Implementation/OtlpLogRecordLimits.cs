// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Contains a snapshot of log record limits and their warning state.
/// </summary>
internal sealed class OtlpLogRecordLimits
{
    internal OtlpLogRecordLimits(
        LogRecordLimitOptions logRecordLimitOptions,
        LimitExceededWarningRateLimiter? warningRateLimiter = null)
    {
        this.WarningTracker = new LimitExceededWarningTracker(
            warningRateLimiter ?? new LimitExceededWarningRateLimiter());
        this.AttributeValueLengthLimit = logRecordLimitOptions.AttributeValueLengthLimit;
        this.AttributeCountLimit = logRecordLimitOptions.AttributeCountLimit;
    }

    internal int? AttributeValueLengthLimit { get; }

    internal int AttributeCountLimit { get; }

    internal LimitExceededWarningTracker WarningTracker { get; }
}
