﻿// <copyright file="MeasureMetricHandle.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Context;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Handle to the metrics Measure with the defined <see cref="LabelSet"/>.
    /// </summary>
    /// <typeparam name="T">The type of Measure. Only long and double are supported now.</typeparam>
    public abstract class MeasureMetricHandle<T>
        where T : struct
    {
        /// <summary>
        /// Record the value of the Measure handle.
        /// </summary>
        /// <param name="context">the associated span context.</param>
        /// <param name="value">value by which the Measure handle should be Recorded.</param>
        public abstract void Record(in SpanContext context, T value);

        /// <summary>
        /// Sets the value of the guague handle.
        /// </summary>
        /// <param name="context">the associated distributed context.</param>
        /// <param name="value">value by which the Measure handle should be Recorded.</param>
        public abstract void Record(in DistributedContext context, T value);
    }
}
