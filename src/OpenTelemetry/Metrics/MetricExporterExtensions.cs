// <copyright file="MetricExporterExtensions.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="BaseExporter{Metric}"/> class.
    /// </summary>
    public static class MetricExporterExtensions
    {
        /// <summary>
        /// Attempts to collect the metrics, blocks the current thread until
        /// metrics collection completed or timed out.
        /// </summary>
        /// <param name="exporter">BaseExporter{Metric} instance on which Collect will be called.</param>
        /// <param name="reader">BaseExportingMetricReader instance on which Collect will be called.</param>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when metrics collection succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety.
        /// </remarks>
        public static bool Collect(this BaseExporter<Metric> exporter, BaseExportingMetricReader reader, int timeoutMilliseconds = Timeout.Infinite)
        {
            if (exporter == null)
            {
                throw new ArgumentNullException(nameof(exporter));
            }

            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "timeoutMilliseconds should be non-negative or Timeout.Infinite.");
            }

            using (PullMetricScope.Begin())
            {
                return reader.Collect(timeoutMilliseconds);
            }
        }
    }
}
