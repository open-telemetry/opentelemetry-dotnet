// <copyright file="Exemplar.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Represents a Exemplar data.
    /// </summary>
    public class Exemplar
    {
        /// <summary>
        /// Gets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; internal set; }

        /// <summary>
        /// Gets the TraceId.
        /// </summary>
        public String TraceId { get; internal set; }

        /// <summary>
        /// Gets the SpanId.
        /// </summary>
        public String SpanId { get; internal set; }

        /// <summary>
        /// Gets the SpanId.
        /// </summary>
        public double DoubleValue { get; internal set; }

        /// <summary>
        /// Gets the SpanId.
        /// </summary>
        public long LongValue { get; internal set; }
    }
}
