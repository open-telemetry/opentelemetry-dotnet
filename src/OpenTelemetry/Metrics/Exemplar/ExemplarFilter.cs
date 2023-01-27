// <copyright file="ExemplarFilter.cs" company="OpenTelemetry Authors">
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
/// The base class for defining Exemplar Filter.
/// </summary>
internal abstract class ExemplarFilter
{
    /// <summary>
    /// Gets the ExemplarFilter which never samples any measurements.
    /// </summary>
    public static ExemplarFilter AlwaysOff { get; } = new AlwaysOffExemplarFilter();

    /// <summary>
    /// Gets the ExemplarFilter which samples all measurements.
    /// </summary>
    public static ExemplarFilter AlwaysOn { get; } = new AlwaysOnExemplarFilter();

    /// <summary>
    /// Gets the ExemplarFilter which samples all measurements that are made
    /// inside context of a sampled Activity.
    /// </summary>
    public static ExemplarFilter TraceBased { get; } = new TraceBasedExemplarFilter();

    /// <summary>
    /// Determines if a given measurement is eligible for being
    /// considered for becoming Exemplar.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    /// <returns>
    /// Returns
    /// <c>true</c> to indicate this measurement is eligible to become Exemplar
    /// and will be given to an ExemplarReservoir.
    /// Reservoir may further sample, so a true here does not mean that this
    /// measurement will become an exemplar, it just means it'll be
    /// eligible for being Exemplar.
    /// <c>false</c> to indicate this measurement is not eligible to become Exemplar
    /// and will not be given to the ExemplarReservoir.
    /// </returns>
    public virtual bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        return false;
    }

    /// <summary>
    /// Determines if a given measurement is eligible for being
    /// considered for becoming Exemplar.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    /// <returns>
    /// Returns
    /// <c>true</c> to indicate this measurement is eligible to become Exemplar
    /// and will be given to an ExemplarReservoir.
    /// Reservoir may further sample, so a true here does not mean that this
    /// measurement will become an exemplar, it just means it'll be
    /// eligible for being Exemplar.
    /// <c>false</c> to indicate this measurement is not eligible to become Exemplar
    /// and will not be given to the ExemplarReservoir.
    /// </returns>
    public virtual bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        return false;
    }
}
