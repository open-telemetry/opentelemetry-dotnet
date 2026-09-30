// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Contains general attribute limit options.
/// </summary>
/// <remarks>
/// <para>
/// These limits are used when <see cref="Trace.SpanLimitOptions"/> or
/// <see cref="Logs.LogRecordLimitOptions"/> do not set their own value.
/// </para>
/// </remarks>
public sealed class AttributeLimitOptions
{
    internal const string AttributeValueLengthLimitEnvVarKey = "OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT";
    internal const string AttributeCountLimitEnvVarKey = "OTEL_ATTRIBUTE_COUNT_LIMIT";
    internal const int DefaultCountLimit = 128;

    internal AttributeLimitOptions()
    {
        this.AttributeCountLimit = DefaultCountLimit;
    }

    internal AttributeLimitOptions(IConfiguration configuration)
    {
        this.AttributeCountLimit = configuration
            .TryGetIntValue(OpenTelemetrySdkEventSource.Log, AttributeCountLimitEnvVarKey, out var countLimit)
                ? countLimit
                : DefaultCountLimit;

        if (configuration.TryGetIntValue(
            OpenTelemetrySdkEventSource.Log,
            AttributeValueLengthLimitEnvVarKey,
            out var lengthLimit))
        {
            this.AttributeValueLengthLimit = lengthLimit;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed attribute count. Attributes added after
    /// the limit is reached are dropped. The default value is 128.
    /// </summary>
    /// <remarks>
    /// Valid values are non-negative.
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
    /// Gets or sets the maximum allowed attribute value size. Longer string and
    /// byte array values are truncated. The default value is
    /// <see langword="null"/>, which means no limit.
    /// </summary>
    /// <remarks>
    /// Valid values are non-negative.
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
