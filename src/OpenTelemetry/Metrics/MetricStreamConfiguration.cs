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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Stores configuration for a MetricStream.
    /// </summary>
    public class MetricStreamConfiguration
    {
        private bool isClosed;

        /// <summary>
        /// Gets the drop configuration.
        /// </summary>
        /// <remarks>
        /// Note: All metrics for the given instrument will be dropped (not
        /// collected).
        /// </remarks>
        public static MetricStreamConfiguration Drop { get; } = new MetricStreamConfiguration();

        /// <summary>
        /// Gets or sets the optional name of the metric stream.
        /// </summary>
        /// <remarks>
        /// Note: If not provided the instrument name will be used.
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the optional description of the metric stream.
        /// </summary>
        /// <remarks>
        /// Note: If note provided the instrument description will be used.
        /// </remarks>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the optional tag keys to include in the metric stream.
        /// </summary>
        /// <remarks>
        /// Note: If provided any metrics for the instrument which do not match
        /// all the tag keys will be dropped (not collected).
        /// </remarks>
        public string[] TagKeys { get; set; }

        internal string[] CopiedTagKeys { get; private set; }

        // TODO: MetricPoints caps can be configured here on
        // a per stream basis, when we add such a capability
        // in the future.

        internal void ValidateAndClose()
        {
            if (!this.isClosed)
            {
                this.isClosed = true;

                if (this.Name != null)
                {
                    if (!MeterProviderBuilderSdk.IsValidViewName(this.Name))
                    {
                        throw new ArgumentException($"Custom view name {this.Name} is invalid.", nameof(this.Name));
                    }
                }

                if (this.TagKeys != null)
                {
                    string[] copy = new string[this.TagKeys.Length];
                    this.TagKeys.AsSpan().CopyTo(copy);
                    this.CopiedTagKeys = copy;
                }

                this.OnValidateAndClose();
            }
        }

        internal virtual void OnValidateAndClose()
        {
        }
    }
}
