// <copyright file="SdkLimitOptions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class SdkLimitOptions
{
    private int? spanAttributeValueLengthLimit;
    private int? spanAttributeCountLimit;
    private int? spanEventAttributeCountLimit;
    private int? spanLinkAttributeCountLimit;

    internal SdkLimitOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal SdkLimitOptions(IConfiguration configuration)
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#attribute-limits
        SetIntConfigValue(configuration, "OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.AttributeValueLengthLimit = value);
        SetIntConfigValue(configuration, "OTEL_ATTRIBUTE_COUNT_LIMIT", value => this.AttributeCountLimit = value);

        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#span-limits
        SetIntConfigValue(configuration, "OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.SpanAttributeValueLengthLimit = value);
        SetIntConfigValue(configuration, "OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", value => this.SpanAttributeCountLimit = value);
        SetIntConfigValue(configuration, "OTEL_SPAN_EVENT_COUNT_LIMIT", value => this.SpanEventCountLimit = value);
        SetIntConfigValue(configuration, "OTEL_SPAN_LINK_COUNT_LIMIT", value => this.SpanLinkCountLimit = value);
        SetIntConfigValue(configuration, "OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", value => this.SpanEventAttributeCountLimit = value);
        SetIntConfigValue(configuration, "OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", value => this.SpanLinkAttributeCountLimit = value);
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
        get => this.spanAttributeValueLengthLimit ?? this.AttributeValueLengthLimit;
        set => this.spanAttributeValueLengthLimit = value;
    }

    /// <summary>
    /// Gets or sets the maximum allowed span attribute count.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="AttributeCountLimit"/> setting for spans if specified.
    /// </remarks>
    public int? SpanAttributeCountLimit
    {
        get => this.spanAttributeCountLimit ?? this.AttributeCountLimit;
        set => this.spanAttributeCountLimit = value;
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
        get => this.spanEventAttributeCountLimit ?? this.SpanAttributeCountLimit;
        set => this.spanEventAttributeCountLimit = value;
    }

    /// <summary>
    /// Gets or sets the maximum allowed span link attribute count.
    /// </summary>
    /// <remarks>
    /// Note: Overrides the <see cref="SpanAttributeCountLimit"/> setting for span links if specified.
    /// </remarks>
    public int? SpanLinkAttributeCountLimit
    {
        get => this.spanLinkAttributeCountLimit ?? this.SpanAttributeCountLimit;
        set => this.spanLinkAttributeCountLimit = value;
    }

    private static void SetIntConfigValue(IConfiguration configuration, string key, Action<int> setter)
    {
        if (configuration.TryGetIntValue(key, out var result))
        {
            setter(result);
        }
    }
}
