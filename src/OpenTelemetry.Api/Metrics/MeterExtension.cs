// <copyright file="MeterExtension.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;

#nullable enable

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension class for additional instruments.
    /// </summary>
    public static class MeterExtension
    {
        /// <summary>
        /// Create a Timer instrument.
        /// </summary>
        /// <typeparam name="T">Support <c>int</c>, <c>long</c>, <c>double</c>.</typeparam>
        /// <param name="meter"><see cref="Meter"/>.</param>
        /// <param name="name">Name of instrument.</param>
        /// <param name="description">Description of instrument.</param>
        /// <returns>Returns <see cref="Timer{T}"/> instrument.</returns>
        public static Timer<T> CreateTimer<T>(this Meter meter, string name, string? description = null)
            where T : struct
        {
            return new Timer<T>(meter, name, description);
        }
    }
}
