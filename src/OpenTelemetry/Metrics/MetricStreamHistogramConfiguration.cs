// <copyright file="MetricStreamHistogramConfiguration.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Stores configuration for a histogram MetricStream.
    /// </summary>
    public sealed class MetricStreamHistogramConfiguration : MetricStreamConfiguration
    {
        internal MetricStreamHistogramConfiguration(
            string name = null,
            string description = null,
            string[] tagKeys = null,
            double[] boundaries = null)
            : base(name, description, tagKeys)
        {
            if (boundaries != null)
            {
                if (!IsSortedAndDistinct(boundaries))
                {
                    throw new ArgumentException("Histogram boundaries must be in ascending order with distinct values.", nameof(boundaries));
                }

                double[] copy = new double[boundaries.Length];
                boundaries.AsSpan().CopyTo(copy);
                this.RawBoundaries = copy;
            }
        }

        /// <summary>
        /// Gets the optional boundaries of the histogram metric stream.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>The array must be in ascending order with distinct values.</item>
        /// <item>An empty array would result in no histogram buckets being calculated.</item>
        /// <item>A null value would result in default bucket boundaries being used.</item>
        /// </list>
        /// </remarks>
        public IReadOnlyList<double> Boundaries => this.RawBoundaries;

        internal double[] RawBoundaries { get; }

        private static bool IsSortedAndDistinct(double[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] <= values[i - 1])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
