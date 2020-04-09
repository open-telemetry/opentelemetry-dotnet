// <copyright file="MeasureMetric.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Context;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Measure instrument.
    /// </summary>
    /// <typeparam name="T">The type of measure. Only long and double are supported now.</typeparam>
    public abstract class MeasureMetric<T>
        where T : struct
    {
        /// <summary>
        /// Records a measure.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value to record.</param>
        /// <param name="labelset">The labelset associated with this value.</param>
        public void Record(in SpanContext context, T value, LabelSet labelset) => this.Bind(labelset).Record(context, value);

        /// <summary>
        /// Records a measure.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value to record.</param>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        public void Record(in SpanContext context, T value, IEnumerable<KeyValuePair<string, string>> labels) => this.Bind(labels).Record(context, value);

        /// <summary>
        /// Records a measure.
        /// </summary>
        /// <param name="context">the associated distributed context.</param>
        /// <param name="value">value to record.</param>
        /// <param name="labelset">The labelset associated with this value.</param>
        public void Record(in DistributedContext context, T value, LabelSet labelset) => this.Bind(labelset).Record(context, value);

        /// <summary>
        /// Records a measure.
        /// </summary>
        /// <param name="context">the associated distributed context.</param>
        /// <param name="value">value to record.</param>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        public void Record(in DistributedContext context, T value, IEnumerable<KeyValuePair<string, string>> labels) => this.Bind(labels).Record(context, value);

        /// <summary>
        /// Gets the bound measure metric with given labelset.
        /// </summary>
        /// <param name="labelset">The labelset from which bound instrument should be constructed.</param>
        /// <returns>The bound measure metric.</returns>
        public abstract BoundMeasureMetric<T> Bind(LabelSet labelset);

        /// <summary>
        /// Gets the bound measure metric with given labelset.
        /// </summary>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        /// <returns>The bound measure metric.</returns>
        public abstract BoundMeasureMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels);
    }
}
