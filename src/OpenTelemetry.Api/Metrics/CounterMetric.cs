// <copyright file="CounterMetric.cs" company="OpenTelemetry Authors">
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
    /// Counter instrument.
    /// </summary>
    /// <typeparam name="T">The type of counter. Only long and double are supported now.</typeparam>
    public abstract class CounterMetric<T>
        where T : struct
    {
        /// <summary>
        /// Adds or Increments the counter.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value by which the counter should be incremented.</param>
        /// <param name="labelset">The labelset associated with this value.</param>
        public abstract void Add(in SpanContext context, T value, LabelSet labelset);

        /// <summary>
        /// Adds or Increments the counter.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value by which the counter should be incremented.</param>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        public abstract void Add(in SpanContext context, T value, IEnumerable<KeyValuePair<string, string>> labels);

        /// <summary>
        /// Adds or Increments the counter.
        /// </summary>
        /// <param name="context">the associated distributed context.</param>
        /// <param name="value">value by which the counter should be incremented.</param>
        /// <param name="labelset">The labelset associated with this value.</param>
        public abstract void Add(in DistributedContext context, T value, LabelSet labelset);

        /// <summary>
        /// Adds or Increments the counter.
        /// </summary>
        /// <param name="context">the associated distributed context.</param>
        /// <param name="value">value by which the counter should be incremented.</param>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        public abstract void Add(in DistributedContext context, T value, IEnumerable<KeyValuePair<string, string>> labels);

        /// <summary>
        /// Gets the bound counter metric with given labelset.
        /// </summary>
        /// <param name="labelset">The labelset from which bound instrument should be constructed.</param>
        /// <returns>The bound counter metric.</returns>
        public abstract BoundCounterMetric<T> Bind(LabelSet labelset);

        /// <summary>
        /// Gets the bound counter metric with given labels.
        /// </summary>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        /// <returns>The bound counter metric.</returns>
        public abstract BoundCounterMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels);
    }
}
