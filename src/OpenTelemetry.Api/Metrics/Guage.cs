﻿// <copyright file="Guage.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Gauge instrument.
    /// </summary>
    /// <typeparam name="T">The type of gauge. Only long and double are supported now.</typeparam>
    public abstract class Guage<T>
        where T : struct
    {
        /// <summary>
        /// Sets the value of the guague.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value to which the Gauge should be set.</param>
        /// <param name="labelset">The labelset associated with this value.</param>
        public abstract void Set(in SpanContext context, T value, LabelSet labelset);

        /// <summary>
        /// Sets the guague.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value to which the Gauge should be set.</param>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        public abstract void Set(in SpanContext context, T value, IEnumerable<KeyValuePair<string, string>> labels);

        /// <summary>
        /// Gets the handle with given labelset.
        /// </summary>
        /// <param name="labelset">The labelset from which handle should be constructed.</param>
        /// <returns>The handle.</returns>
        public abstract CounterHandle<T> GetHandle(LabelSet labelset);

        /// <summary>
        /// Gets the handle with given labels.
        /// </summary>
        /// <param name="labels">The labels or dimensions associated with this value.</param>
        /// <returns>The handle.</returns>
        public abstract CounterHandle<T> GetHandle(IEnumerable<KeyValuePair<string, string>> labels);
    }
}
