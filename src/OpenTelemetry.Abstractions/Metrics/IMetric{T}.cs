// <copyright file="IMetric{T}.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    using System;
    using System.Collections.Generic;
    using OpenTelemetry.Resources;

    /// <summary>
    /// Base interface for all metrics defined in this package.
    /// </summary>
    /// <typeparam name="T">Type of the time series of the metric.</typeparam>
    public interface IMetric<T>
    {
        /// <summary>
        /// Creates a time series and returns a time series if the specified labelValues
        /// is not already associated with this metric, else returns an existing time series.
        ///
        /// It is recommended to keep a reference to the time series instead of always calling this
        /// method for every operations.
        /// </summary>
        /// <param name="labelValues">
        /// The list of label values. The number of label values must be the same to
        /// that of the label keys passed to.
        /// </param>
        /// <returns>A time series the value of single metric.</returns>
        T GetOrCreateTimeSeries(IEnumerable<string> labelValues);

        /// <summary>
        /// Returns a time series for a metric with all labels not set (default label values).
        /// </summary>
        /// <returns>A time series for a metric with all labels not set (default label values)</returns>
        T GetDefaultTimeSeries();

        /// <summary>
        /// Sets a callback that gets executed every time before exporting this metric. Used to
        /// implement pull-based metric.
        ///
        /// Evaluation is deferred until needed, if this <see cref="IMetric{T}"/> is not exported then it will never be called.
        /// </summary>
        /// <param name="metricUpdater">The callback to be executed before export.</param>
        void SetCallback(Action metricUpdater);

        /// <summary>
        /// Removes the time series from the metric, if it is present. i.e. references to previous time series
        /// are invalid (not part of the metric).
        ///
        /// If value is missing for one of the predefined keys null must be used for that value.
        /// </summary>
        /// <param name="labelValues">The list of label values</param>
        void RemoveTimeSeries(IEnumerable<string> labelValues);

        /// <summary>
        /// Removes all time series from the metric. i.e. references to all previous time series are invalid (not part of the metric).
        /// </summary>
        void Clear();
    }
}
