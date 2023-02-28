// <copyright file="ExemplarReservoir.cs" company="OpenTelemetry Authors">
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
namespace OpenTelemetry.Metrics;

/// <summary>
/// The base class for defining Exemplar Reservoir.
/// </summary>
internal abstract class ExemplarReservoir
{
    /// <summary>
    /// Offers measurement to the reservoir.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    /// <param name="index">The histogram bucket index where this measurement is going to be stored.
    /// This is optional and is only relevant for Histogram with buckets.</param>
    public abstract void Offer(long value, ReadOnlySpan<KeyValuePair<string, object>> tags, int index = default);

    /// <summary>
    /// Offers measurement to the reservoir.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    /// <param name="index">The histogram bucket index where this measurement is going to be stored.
    /// This is optional and is only relevant for Histogram with buckets.</param>
    public abstract void Offer(double value, ReadOnlySpan<KeyValuePair<string, object>> tags, int index = default);

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <param name="actualTags">The actual tags that are part of the metric. Exemplars are
    /// only expected to contain any filtered tags, so this will allow the reservoir
    /// prepare the filtered tags from all the tags it is given by doing the
    /// equivalent of filtered tags = all tags - actual tags.
    /// </param>
    /// <param name="reset">Flag to indicate if the reservoir should be reset after this call.</param>
    /// <returns>Array of Exemplars.</returns>
    public abstract Exemplar[] Collect(ReadOnlyTagCollection actualTags, bool reset);
}
