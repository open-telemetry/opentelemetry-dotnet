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

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// The base class for defining Exemplar Filter.
/// </summary>
/// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
public
#else
/// <summary>
/// The base class for defining Exemplar Filter.
/// </summary>
internal
#endif
    abstract class ExemplarFilter
{
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
    public abstract bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

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
    public abstract bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags);
}
