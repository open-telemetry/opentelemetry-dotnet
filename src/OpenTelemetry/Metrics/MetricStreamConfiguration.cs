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

namespace OpenTelemetry.Metrics
{
    // TODO: can be optimized like MetricType
    internal enum Aggregation
    {
#pragma warning disable SA1602 // Enumeration items should be documented
        Default,
        Drop,
        Sum,
        LastValue,
        Histogram,
#pragma warning restore SA1602 // Enumeration items should be documented
    }

    /// <summary>
    /// Holds the configuration for a MetricStream.
    /// </summary>
    public class MetricStreamConfiguration
    {
        public static readonly MetricStreamConfiguration Drop = new DropConfiguration();

        public string Name { get; set; }

        public string Description { get; set; }

        public string[] TagKeys { get; set; }

        internal virtual Aggregation Aggregation { get; set; }

        // TODO: MetricPoints caps can be configured here on
        // a per stream basis, when we add such a capability
        // in the future.

        private sealed class DropConfiguration : MetricStreamConfiguration
        {
            internal override Aggregation Aggregation
            {
                get => Aggregation.Drop;
                set { }
            }
        }
    }
}
