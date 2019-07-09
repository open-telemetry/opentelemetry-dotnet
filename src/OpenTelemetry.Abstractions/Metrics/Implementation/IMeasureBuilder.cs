// <copyright file="IMeasureBuilder.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Metrics.Implementation
{
    /// <summary>
    /// Builder for the <see cref="IMeasure"/>.
    /// </summary>
    public interface IMeasureBuilder
    {
        /// <summary>
        /// Sets the description of the <see cref="IMeasure"/>.
        /// </summary>
        /// <param name="description">The detailed description of the <see cref="IMeasure"/>.</param>
        /// <returns>This builder object.</returns>
        IMeasureBuilder SetDescription(string description);

        /// <summary>
        /// Sets the description of the <see cref="IMeasure"/>.
        /// </summary>
        /// <param name="unit">The detailed description of the <see cref="IMeasure"/>.</param>
        /// <returns>This builder object.</returns>
        IMeasureBuilder SetUnit(string unit);

        /// <summary>
        /// Sets the corresponding to the underlying value of the <see cref="IMeasure"/>.
        /// </summary>
        /// <param name="type">The detailed description of the <see cref="IMeasure"/>.</param>
        /// <returns>This builder object.</returns>
        IMeasureBuilder SetType(MeasureType type);

        /// <summary>
        /// Builds the <see cref="IMeasure"/>.
        /// </summary>
        /// <returns>The <see cref="IMeasure"/> object.</returns>
        IMeasure Build();
    }
}
