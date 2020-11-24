// <copyright file="NoopCounterMetric.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// A no-op counter instrument.
    /// </summary>
    /// <typeparam name="T">The type of counter. Only long and double are supported now.</typeparam>
    [Obsolete("Metrics API/SDK is not recommended for production. See https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501 for more information on metrics support.")]
    public sealed class NoopCounterMetric<T> : CounterMetric<T>
        where T : struct
    {
        /// <summary>
        /// No op counter instance.
        /// </summary>
        public static readonly NoopCounterMetric<T> Instance = new NoopCounterMetric<T>();

        /// <inheritdoc/>
        public override void Add(in SpanContext context, T value, LabelSet labelset)
        {
        }

        /// <inheritdoc/>
        public override void Add(in SpanContext context, T value, IEnumerable<KeyValuePair<string, string>> labels)
        {
        }

        /// <inheritdoc/>
        public override void Add(in Baggage context, T value, LabelSet labelset)
        {
        }

        /// <inheritdoc/>
        public override void Add(in Baggage context, T value, IEnumerable<KeyValuePair<string, string>> labels)
        {
        }

        /// <inheritdoc/>
        public override BoundCounterMetric<T> Bind(LabelSet labelset)
        {
            return NoopBoundCounterMetric<T>.Instance;
        }

        /// <inheritdoc/>
        public override BoundCounterMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return NoopBoundCounterMetric<T>.Instance;
        }
    }
}
