// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains span limit options.
/// </summary>
/// <remarks>
/// When this type is constructed by the SDK, the
/// <c>OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT</c>, <c>OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT</c>,
/// <c>OTEL_SPAN_EVENT_COUNT_LIMIT</c>, <c>OTEL_SPAN_LINK_COUNT_LIMIT</c>,
/// <c>OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT</c>, and <c>OTEL_LINK_ATTRIBUTE_COUNT_LIMIT</c>
/// environment variables are applied to properties which have not been set in
/// code.
/// </remarks>
public sealed class SpanLimitOptions
{
    internal const string AttributeValueLengthLimitEnvVarKey = "OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT";
    internal const string AttributeCountLimitEnvVarKey = "OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT";
    internal const string EventCountLimitEnvVarKey = "OTEL_SPAN_EVENT_COUNT_LIMIT";
    internal const string LinkCountLimitEnvVarKey = "OTEL_SPAN_LINK_COUNT_LIMIT";
    internal const string AttributePerEventCountLimitEnvVarKey = "OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT";
    internal const string AttributePerLinkCountLimitEnvVarKey = "OTEL_LINK_ATTRIBUTE_COUNT_LIMIT";

    internal SpanLimitOptions()
    {
        this.AttributeCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.EventCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.LinkCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.AttributePerEventCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.AttributePerLinkCountLimit = AttributeLimitOptions.DefaultCountLimit;
    }

    internal SpanLimitOptions(IConfiguration configuration, AttributeLimitOptions attributeLimits)
    {
        this.AttributeCountLimit = configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, AttributeCountLimitEnvVarKey, out var spanCount)
            ? spanCount
            : attributeLimits.AttributeCountLimit;

        this.AttributeValueLengthLimit = configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, AttributeValueLengthLimitEnvVarKey, out var spanLength)
            ? spanLength
            : attributeLimits.AttributeValueLengthLimit;

        this.AttributePerEventCountLimit = configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, AttributePerEventCountLimitEnvVarKey, out var eventAttrCount)
            ? eventAttrCount
            : attributeLimits.AttributeCountLimit;

        this.AttributePerLinkCountLimit = configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, AttributePerLinkCountLimitEnvVarKey, out var linkAttrCount)
            ? linkAttrCount
            : attributeLimits.AttributeCountLimit;

        this.EventCountLimit = configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, EventCountLimitEnvVarKey, out var eventCount)
            ? eventCount
            : AttributeLimitOptions.DefaultCountLimit;

        this.LinkCountLimit = configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, LinkCountLimitEnvVarKey, out var linkCount)
            ? linkCount
            : AttributeLimitOptions.DefaultCountLimit;
    }

    /// <summary>
    /// Gets or sets the maximum span attribute count. Attributes
    /// added after the limit is reached are dropped.
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
    /// Gets or sets the maximum allowed attribute value size of span, span event,
    /// and span link attribute values. Longer string and byte array values are truncated.
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

    /// <summary>
    /// Gets or sets the maximum allowed attribute per span event count. Attributes
    /// added after the limit is reached are dropped.
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
    public int AttributePerEventCountLimit
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, nameof(value), min: 0);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed attribute per span link count. Attributes
    /// added after the limit is reached are dropped.
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
    public int AttributePerLinkCountLimit
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, nameof(value), min: 0);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed span event count. Events added after
    /// the limit is reached are dropped. The default value is 128.
    /// </summary>
    /// <remarks>
    /// Valid values are non-negative.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public int EventCountLimit
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, nameof(value), min: 0);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed span link count. Links added after
    /// the limit is reached are dropped. The default value is 128.
    /// </summary>
    /// <remarks>
    /// Valid values are non-negative.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public int LinkCountLimit
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, nameof(value), min: 0);
            field = value;
        }
    }
}
