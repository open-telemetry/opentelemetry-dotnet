// <copyright file="MetricStreamConfiguration.cs" company="OpenTelemetry Authors">
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
    /// Stores configuration for a MetricStream.
    /// </summary>
    public class MetricStreamConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricStreamConfiguration"/> class.
        /// </summary>
        /// <param name="name">Optional view name. See <see cref="Name"/> for more details.</param>
        /// <param name="description">Optional view description. See <see cref="Description"/> for more details.</param>
        /// <param name="tagKeys">Optional tag keys. See <see cref="TagKeys"/> for more details.</param>
        public MetricStreamConfiguration(
            string name = null,
            string description = null,
            string[] tagKeys = null)
        {
            this.Description = description;

            if (name != null)
            {
                if (!MeterProviderBuilderSdk.IsValidViewName(name))
                {
                    throw new ArgumentException($"Custom view name {name} is invalid.", nameof(name));
                }

                this.Name = name;
            }

            if (tagKeys != null)
            {
                string[] copy = new string[tagKeys.Length];
                tagKeys.AsSpan().CopyTo(copy);
                this.RawTagKeys = copy;
            }
        }

        /// <summary>
        /// Gets the drop configuration.
        /// </summary>
        /// <remarks>
        /// Note: All metrics for the given instrument will be dropped (not
        /// collected).
        /// </remarks>
        public static MetricStreamConfiguration Drop { get; } = new MetricStreamConfiguration();

        /// <summary>
        /// Gets the optional name of the metric stream.
        /// </summary>
        /// <remarks>
        /// Note: If not provided the instrument name will be used.
        /// </remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the optional description of the metric stream.
        /// </summary>
        /// <remarks>
        /// Note: If note provided the instrument description will be used.
        /// </remarks>
        public string Description { get; }

        /// <summary>
        /// Gets the optional tag keys to include in the metric stream.
        /// </summary>
        /// <remarks>
        /// Note: If provided any metrics for the instrument which do not match
        /// all the tag keys will be dropped (not collected).
        /// </remarks>
        public IReadOnlyList<string> TagKeys => this.RawTagKeys;

        internal string[] RawTagKeys { get; }

        // TODO: MetricPoints caps can be configured here on
        // a per stream basis, when we add such a capability
        // in the future.

        /// <summary>
        /// Creates a histogram metric stream configuration.
        /// </summary>
        /// <param name="boundaries">Optional histogram boundaries. See <see cref="MetricStreamHistogramConfiguration.Boundaries"/> for details.</param>
        /// <returns><see cref="MetricStreamHistogramConfiguration"/>.</returns>
        public static MetricStreamHistogramConfiguration CreateHistogramConfiguration(double[] boundaries)
            => CreateHistogramConfiguration(
                name: null,
                description: null,
                tagKeys: null,
                boundaries: boundaries);

        /// <summary>
        /// Creates a histogram metric stream configuration.
        /// </summary>
        /// <param name="name">Optional view name. See <see cref="Name"/> for more details.</param>
        /// <param name="description">Optional view description. See <see cref="Description"/> for more details.</param>
        /// <param name="tagKeys">Optional tag keys. See <see cref="TagKeys"/> for more details.</param>
        /// <param name="boundaries">Optional histogram boundaries. See <see cref="MetricStreamHistogramConfiguration.Boundaries"/> for details.</param>
        /// <returns><see cref="MetricStreamHistogramConfiguration"/>.</returns>
        public static MetricStreamHistogramConfiguration CreateHistogramConfiguration(
            string name = null,
            string description = null,
            string[] tagKeys = null,
            double[] boundaries = null)
        {
            return new MetricStreamHistogramConfiguration(
                name,
                description,
                tagKeys,
                boundaries);
        }
    }
}
