// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class SdkLimitOptions
{
    private const int DefaultSdkLimit = 128;
    private int? spanAttributeValueLengthLimit;
    private bool spanAttributeValueLengthLimitSet;
    private int? spanAttributeCountLimit;
    private bool spanAttributeCountLimitSet;
    private int? spanEventAttributeCountLimit;
    private bool spanEventAttributeCountLimitSet;
    private int? spanLinkAttributeCountLimit;
    private bool spanLinkAttributeCountLimitSet;
    private int? logRecordAttributeValueLengthLimit;
    private bool logRecordAttributeValueLengthLimitSet;
    private int? logRecordAttributeCountLimit;
    private bool logRecordAttributeCountLimitSet;

    public SdkLimitOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal SdkLimitOptions(IConfiguration configuration)
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#attribute-limits
        SetIntConfigValue(configuration, "OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.AttributeValueLengthLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_ATTRIBUTE_COUNT_LIMIT", value => this.AttributeCountLimit = value, DefaultSdkLimit);

        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#span-limits
        SetIntConfigValue(configuration, "OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.SpanAttributeValueLengthLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", value => this.SpanAttributeCountLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_SPAN_EVENT_COUNT_LIMIT", value => this.SpanEventCountLimit = value, DefaultSdkLimit);
        SetIntConfigValue(configuration, "OTEL_SPAN_LINK_COUNT_LIMIT", value => this.SpanLinkCountLimit = value, DefaultSdkLimit);
        SetIntConfigValue(configuration, "OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", value => this.SpanEventAttributeCountLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", value => this.SpanLinkAttributeCountLimit = value, null);

        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#logrecord-limits
        SetIntConfigValue(configuration, "OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.LogRecordAttributeValueLengthLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT", value => this.LogRecordAttributeCountLimit = value, null);
    }

    /// <summary>
    /// Gets or sets the maximum allowed attribute value size.
    /// </summary>
    public int? AttributeValueLengthLimit { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed attribute count.
    /// </summary>
    public int? AttributeCountLimit { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed span attribute value size.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="AttributeValueLengthLimit"/> setting for spans if specified.
    /// </remarks>
    public int? SpanAttributeValueLengthLimit
    {
        get => this.spanAttributeValueLengthLimitSet ? this.spanAttributeValueLengthLimit : this.AttributeValueLengthLimit;
        set
        {
            this.spanAttributeValueLengthLimitSet = true;
            this.spanAttributeValueLengthLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed span attribute count.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="AttributeCountLimit"/> setting for spans if specified.
    /// </remarks>
    public int? SpanAttributeCountLimit
    {
        get => this.spanAttributeCountLimitSet ? this.spanAttributeCountLimit : this.AttributeCountLimit;
        set
        {
            this.spanAttributeCountLimitSet = true;
            this.spanAttributeCountLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed span event count.
    /// </summary>
    public int? SpanEventCountLimit { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed span link count.
    /// </summary>
    public int? SpanLinkCountLimit { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed span event attribute count.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="SpanAttributeCountLimit"/> setting for span events if specified.
    /// </remarks>
    public int? SpanEventAttributeCountLimit
    {
        get => this.spanEventAttributeCountLimitSet ? this.spanEventAttributeCountLimit : this.SpanAttributeCountLimit;
        set
        {
            this.spanEventAttributeCountLimitSet = true;
            this.spanEventAttributeCountLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed span link attribute count.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="SpanAttributeCountLimit"/> setting for span links if specified.
    /// </remarks>
    public int? SpanLinkAttributeCountLimit
    {
        get => this.spanLinkAttributeCountLimitSet ? this.spanLinkAttributeCountLimit : this.SpanAttributeCountLimit;
        set
        {
            this.spanLinkAttributeCountLimitSet = true;
            this.spanLinkAttributeCountLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed log record attribute value size.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="AttributeValueLengthLimit"/> setting for log records if specified.
    /// </remarks>
    public int? LogRecordAttributeValueLengthLimit
    {
        get => this.logRecordAttributeValueLengthLimitSet ? this.logRecordAttributeValueLengthLimit : this.AttributeValueLengthLimit;
        set
        {
            this.logRecordAttributeValueLengthLimitSet = true;
            this.logRecordAttributeValueLengthLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed log record attribute count.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="AttributeCountLimit"/> setting for log records if specified.
    /// </remarks>
    public int? LogRecordAttributeCountLimit
    {
        get => this.logRecordAttributeCountLimitSet ? this.logRecordAttributeCountLimit : this.AttributeCountLimit;
        set
        {
            this.logRecordAttributeCountLimitSet = true;
            this.logRecordAttributeCountLimit = value;
        }
    }

    private static void SetIntConfigValue(IConfiguration configuration, string key, Action<int?> setter, int? defaultValue)
    {
        if (configuration.TryGetIntValue(key, out var result))
        {
            setter(result);
        }
        else if (defaultValue.HasValue)
        {
            setter(defaultValue);
        }
    }
}
