// <copyright file="Exemplar.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Represents an Exemplar data.
/// </summary>
/// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
public
#else
/// <summary>
/// Represents an Exemplar data.
/// </summary>
#pragma warning disable SA1623 // The property's documentation summary text should begin with: `Gets or sets`
internal
#endif
    struct Exemplar
{
    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; internal set; }

    /// <summary>
    /// Gets the TraceId.
    /// </summary>
    public ActivityTraceId? TraceId { get; internal set; }

    /// <summary>
    /// Gets the SpanId.
    /// </summary>
    public ActivitySpanId? SpanId { get; internal set; }

    // TODO: Leverage MetricPointValueStorage
    // and allow double/long instead of double only.

    /// <summary>
    /// Gets the double value.
    /// </summary>
    public double DoubleValue { get; internal set; }

    /// <summary>
    /// Gets the FilteredTags (i.e any tags that were dropped during aggregation).
    /// </summary>
    public List<KeyValuePair<string, object?>>? FilteredTags { get; internal set; }
}

