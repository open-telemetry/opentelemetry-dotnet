// <copyright file="SpanLimits.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace;

/// <summary>
/// Defines the attribute count and length limits for spans.
/// </summary>
public class SpanLimits
{
    /// <summary>
    /// Gets or sets the maximum allowed number of attributes on spans.
    /// </summary>
    public int? AttributeCountLimit { get; set; } = 128;

    /// <summary>
    /// Gets or sets the maximum length for string-valued attributes on spans.
    /// </summary>
    public int? AttributeValueLengthLimit { get; set; } = null;

    // TODO: Implement span event and link limits.
    // Open question:
    // When SpanEventAttributeCountLimit or SpanLinkAttributeCountLimit are null
    // should we fall back to use AttributeCountLimit?
    // Or Should we allow these to be independently configurable?

    // public int? SpanEventCountLimit { get;  set; } = 128;
    // public int? SpanLinkCountLimit { get; set; } = 128;
    // public int? SpanEventAttributeCountLimit { get; set; } = 128;
    // public int? SpanLinkAttributeCountLimit { get; set; } = 128;
}
