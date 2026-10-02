// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains log record limit options.
/// </summary>
/// <remarks>
/// When this type is constructed by the SDK, the
/// <c>OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT</c> and
/// <c>OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT</c> environment variables are applied
/// to properties which have not been set in code.
/// </remarks>
public sealed class LogRecordLimitOptions
{
    internal const string AttributeValueLengthLimitEnvVarKey = "OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT";
    internal const string AttributeCountLimitEnvVarKey = "OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT";

    internal LogRecordLimitOptions()
    {
        this.AttributeCountLimit = AttributeLimitOptions.DefaultCountLimit;
    }

    internal LogRecordLimitOptions(IConfiguration configuration, AttributeLimitOptions attributeLimitOptions)
    {
        this.AttributeCountLimit = configuration.TryGetIntValue(
            OpenTelemetrySdkEventSource.Log,
            AttributeCountLimitEnvVarKey,
            out var logCount)
                ? logCount
                : attributeLimitOptions.AttributeCountLimit;

        this.AttributeValueLengthLimit = configuration.TryGetIntValue(
            OpenTelemetrySdkEventSource.Log,
            AttributeValueLengthLimitEnvVarKey,
            out var logLength)
                ? logLength
                : attributeLimitOptions.AttributeValueLengthLimit;
    }

    /// <summary>
    /// Gets or sets the maximum allowed log record attribute count.
    /// Attributes added after the limit is reached are dropped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Valid values are non-negative.
    /// </para>
    /// <para>
    /// If not set, <see cref="AttributeLimitOptions.AttributeCountLimit"/> is
    /// used, which defaults to 128.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public int AttributeCountLimit
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, nameof(value), min: 0);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed attribute value size. Longer
    /// string and byte array values are truncated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Valid values are non-negative.
    /// </para>
    /// <para>
    /// If not set, <see cref="AttributeLimitOptions.AttributeValueLengthLimit"/>
    /// is used, which defaults to no limit.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public int? AttributeValueLengthLimit
    {
        get;
        set
        {
            if (value.HasValue)
            {
                Guard.ThrowIfOutOfRange(value.Value, nameof(value), min: 0);
            }

            field = value;
        }
    }
}
