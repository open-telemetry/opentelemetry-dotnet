// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains span limit options.
/// </summary>
/// <remarks>
/// Internal until the limits are enforced by every exporter in this repository. Binding to
/// configuration, including the fallback to <see cref="AttributeLimitOptions"/>, is added when
/// <c>SdkLimitOptions</c> in the OTLP exporter is replaced.
/// </remarks>
internal sealed class SpanLimitOptions
{
    internal SpanLimitOptions()
    {
        this.AttributeCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.EventCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.LinkCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.AttributePerEventCountLimit = AttributeLimitOptions.DefaultCountLimit;
        this.AttributePerLinkCountLimit = AttributeLimitOptions.DefaultCountLimit;
    }

    /// <summary>
    /// Gets or sets the maximum allowed span attribute count. Default value: 128.
    /// </summary>
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
    /// Gets or sets the maximum allowed attribute value length for span, event and link
    /// attributes. Default value: no limit.
    /// </summary>
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
    /// Gets or sets the maximum allowed attribute count per span event. Default value: 128.
    /// </summary>
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
    /// Gets or sets the maximum allowed attribute count per span link. Default value: 128.
    /// </summary>
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
    /// Gets or sets the maximum allowed span event count. Default value: 128.
    /// </summary>
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
    /// Gets or sets the maximum allowed span link count. Default value: 128.
    /// </summary>
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
