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
/// The base class defining Exemplar Reservoir.
/// </summary>
internal abstract class ExemplarReservoir
{
    /// <summary>
    /// Offers a measurement to the Reservoir.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    public virtual void Offer(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
    }

    /// <summary>
    /// Offers a measurement to the Reservoir.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    public virtual void Offer(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
    }

    /// <summary>
    /// Collects the Exemplars stored in the Reservoir so far.
    /// </summary>
    /// <returns>List of Exemplars.</returns>
    public virtual Exemplar[] Collect()
    {
        return Array.Empty<Exemplar>();
    }

    /// <summary>
    /// Take snapshot of running exemplars.
    /// </summary>
    public virtual void SnapShot()
    {
    }
}
