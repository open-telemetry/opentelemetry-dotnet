// <copyright file="SpanKind.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Span kind.
    /// </summary>
    public enum SpanKind
    {
        /// <summary>
        /// Span kind was not specified.
        /// </summary>
        Internal = 1,

        /// <summary>
        /// Server span represents request incoming from external component.
        /// </summary>
        Server = 2,

        /// <summary>
        /// Client span represents outgoing request to the external component.
        /// </summary>
        Client = 3,

        /// <summary>
        /// Producer span represents output provided to external components. Unlike client and
        /// server, there is no direct critical path latency relationship between producer and consumer
        /// spans.
        /// </summary>
        Producer = 4,

        /// <summary>
        /// Consumer span represents output received from an external component. Unlike client and
        /// server, there is no direct critical path latency relationship between producer and consumer
        /// spans.
        /// </summary>
        Consumer = 5,
    }
}
