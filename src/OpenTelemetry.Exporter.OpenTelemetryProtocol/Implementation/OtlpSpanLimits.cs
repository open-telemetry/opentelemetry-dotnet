// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Contains a snapshot of span limits and their warning state.
/// </summary>
internal sealed class OtlpSpanLimits
{
    internal OtlpSpanLimits(
        SpanLimitOptions spanLimitOptions,
        AttributeLimitOptions attributeLimitOptions,
        LimitExceededWarningRateLimiter? warningRateLimiter = null)
    {
        this.WarningTracker = new LimitExceededWarningTracker(warningRateLimiter ?? new LimitExceededWarningRateLimiter());

        this.AttributeCountLimit = spanLimitOptions.AttributeCountLimit;
        this.AttributePerEventCountLimit = spanLimitOptions.AttributePerEventCountLimit;
        this.AttributePerLinkCountLimit = spanLimitOptions.AttributePerLinkCountLimit;
        this.EventCountLimit = spanLimitOptions.EventCountLimit;
        this.LinkCountLimit = spanLimitOptions.LinkCountLimit;
        this.ScopeAttributeCountLimit = attributeLimitOptions.AttributeCountLimit;
        this.ScopeAttributeValueLengthLimit = attributeLimitOptions.AttributeValueLengthLimit;
        this.SpanAttributeValueLengthLimit = spanLimitOptions.AttributeValueLengthLimit;
    }

    internal int AttributeCountLimit { get; }

    internal int AttributePerEventCountLimit { get; }

    internal int AttributePerLinkCountLimit { get; }

    internal int EventCountLimit { get; }

    internal int LinkCountLimit { get; }

    internal int ScopeAttributeCountLimit { get; }

    internal int? ScopeAttributeValueLengthLimit { get; }

    internal int? SpanAttributeValueLengthLimit { get; }

    internal LimitExceededWarningTracker WarningTracker { get; }
}
