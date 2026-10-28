// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Contains general attribute limit options.
/// </summary>
/// <remarks>
/// These limits are used when <see cref="Trace.SpanLimitOptions"/> does not set its own value.
/// <para/>
/// Internal until the limits are enforced by every exporter in this repository. Binding to
/// configuration is added when <c>SdkLimitOptions</c> in the OTLP exporter is replaced.
/// </remarks>
internal sealed class AttributeLimitOptions
{
    internal const int DefaultCountLimit = 128;

    internal AttributeLimitOptions()
    {
        this.AttributeCountLimit = DefaultCountLimit;
    }

    /// <summary>
    /// Gets or sets the maximum allowed attribute count. Default value: 128.
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
    /// Gets or sets the maximum allowed attribute value length. Default value: no limit.
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
}
