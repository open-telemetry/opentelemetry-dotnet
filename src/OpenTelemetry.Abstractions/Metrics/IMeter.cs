// <copyright file="IMeter.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using OpenTelemetry.Metrics.Implementation;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Trace;

    /// <summary>
    /// Returns a builder for <see cref="ICounterDouble"/>.
    /// </summary>
    public interface IMeter
    {
        /// <summary>
        /// Gets the builder for <see cref="ICounterDouble"/>.
        /// </summary>
        /// <param name="name">Name of the counter.</param>
        /// <returns>The builder for the <see cref="ICounterDouble"/>.</returns>
        ICounterDoubleBuilder GetCounterDoubleBuilder(string name);

        /// <summary>
        /// Gets the builder for <see cref="ICounterLong"/>.
        /// </summary>
        /// <param name="name">Name of the counter.</param>
        /// <returns>The builder for the <see cref="ICounterLong"/>.</returns>
        ICounterLongBuilder GetCounterLongBuilder(string name);

        /// <summary>
        /// Gets the builder for <see cref="IGaugeDouble"/>.
        /// </summary>
        /// <param name="name">Name of the counter.</param>
        /// <returns>The builder for the <see cref="IGaugeDouble"/>.</returns>
        IGaugeDoubleBuilder GetGaugeDoubleBuilder(string name);

        /// <summary>
        /// Gets the builder for <see cref="IGaugeLong"/>.
        /// </summary>
        /// <param name="name">Name of the counter.</param>
        /// <returns>The builder for the <see cref="IGaugeLong"/>.</returns>
        IGaugeLongBuilder GetGaugeLongBuilder(string name);

        /// <summary>
        /// Gets the builder for the <see cref="IMeasure"/>.
        /// </summary>
        /// <param name="name">The name of the <see cref="IMeasure"/>.</param>
        /// <returns>The <see cref="IMeasureBuilder"/> to build the <see cref="IMeasure"/>.</returns>
        IMeasureBuilder GetMeasureBuilder(string name);

        void Record(IEnumerable<IMeasurement> measurements);

        void Record(IEnumerable<IMeasurement> measurements, ITagContext tagContext);

        void Record(IEnumerable<IMeasurement> measurements, ITagContext tagContext, SpanContext spanContext);
    }
}
