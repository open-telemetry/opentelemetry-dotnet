// <copyright file="IMetricBuilder{T}.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;

    /// <summary>
    /// Metric builder interface.
    /// </summary>
    /// <typeparam name="T">Type of time series in metric.</typeparam>
    public interface IMetricBuilder<T>
    {
        /// <summary>
        /// Sets the description of the <see cref="IMetric{T}"/>.
        /// </summary>
        /// <param name="description">Description of the metric.</param>
        /// <returns>This builder instance.</returns>
        IMetricBuilder<T> SetDescription(string description);

        /// <summary>
        /// Sets the description of the <see cref="IMetric{T}"/>.
        /// </summary>
        /// <param name="unit">Unit of the metric.</param>
        /// <returns>This builder instance.</returns>
        IMetricBuilder<T> SetUnit(string unit);

        /// <summary>
        /// Sets the description of the <see cref="IMetric{T}"/>.
        /// </summary>
        /// <param name="labelKeys">List of keys for dynamic labels.</param>
        /// <returns>This builder instance.</returns>
        IMetricBuilder<T> SetLabelKeys(IEnumerable<LabelKey> labelKeys);

        /// <summary>
        /// Sets the description of the <see cref="IMetric{T}"/>.
        /// </summary>
        /// <param name="constantLabels">Set of labels with values.</param>
        /// <returns>This builder instance.</returns>
        IMetricBuilder<T> SetConstantLabels(IDictionary<LabelKey, string> constantLabels);

        /// <summary>
        /// Sets the description of the <see cref="IMetric{T}"/>.
        /// </summary>
        /// <param name="component">Component reporting this metric.</param>
        /// <returns>This builder instance.</returns>
        IMetricBuilder<T> SetComponent(string component);

        /// <summary>
        /// Builds the <see cref="IMetric{T}"/>.
        /// </summary>
        /// <returns>The new instance of <see cref="IMetric{T}"/>.</returns>
        IMetric<T> Build();
    }
}
