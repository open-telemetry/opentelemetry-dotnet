﻿// <copyright file="IMeasure.cs" company="OpenTelemetry Authors">
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
    /// <summary>
    /// Measure instrument.
    /// </summary>
    public interface IMeasure
    {
        /// <summary>
        /// Records a measure.
        /// </summary>
        /// <param name="value">value by which the counter should be incremented.</param>
        /// <param name="labelset">The labelset associated with this value.</param>
        void Record(int value, LabelSet labelset);

        /// <summary>
        /// Gets the handle with given labelset.
        /// </summary>
        /// <param name="labelset">The labelset from which handle should be constructed.</param>
        /// <returns>The <see cref="IMeasureHandle" /> with label.</returns>
        IMeasureHandle GetHandle(LabelSet labelset);
    }
}
