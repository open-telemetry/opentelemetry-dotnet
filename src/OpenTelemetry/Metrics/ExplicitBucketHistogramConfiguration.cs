// <copyright file="ExplicitBucketHistogramConfiguration.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Stores configuration for a histogram metric stream with explicit bucket boundaries.
    /// </summary>
    public class ExplicitBucketHistogramConfiguration : HistogramConfiguration
    {
        /// <summary>
        /// Gets or sets the optional boundaries of the histogram metric stream.
        /// </summary>
        /// <remarks>
        /// Requirements:
        /// <list type="bullet">
        /// <item>The array must be in ascending order with distinct
        /// values.</item>
        /// <item>An empty array would result in no histogram buckets being
        /// calculated.</item>
        /// <item>A null value would result in default bucket boundaries being
        /// used.</item>
        /// </list>
        /// Note: A copy is made of the provided array.
        /// </remarks>
        public double[] Boundaries
        {
            get
            {
                if (this.CopiedBoundaries != null)
                {
                    double[] copy = new double[this.CopiedBoundaries.Length];
                    this.CopiedBoundaries.AsSpan().CopyTo(copy);
                    return copy;
                }

                return null;
            }

            set
            {
                if (value != null)
                {
                    if (!IsSortedAndDistinct(value))
                    {
                        throw new ArgumentException($"Histogram boundaries are invalid. Histogram boundaries must be in ascending order with distinct values.", nameof(value));
                    }

                    double[] copy = new double[value.Length];
                    value.AsSpan().CopyTo(copy);
                    this.CopiedBoundaries = copy;
                }
                else
                {
                    this.CopiedBoundaries = null;
                }
            }
        }

        internal double[] CopiedBoundaries { get; private set; }

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
