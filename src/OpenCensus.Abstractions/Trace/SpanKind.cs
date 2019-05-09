// <copyright file="SpanKind.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    /// <summary>
    /// Span kind.
    /// </summary>
    public enum SpanKind
    {
        /// <summary>
        /// Span kind was not specified.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Server span represents request incoming from external component.
        /// </summary>
        Server = 1,

        /// <summary>
        /// Client span represents outgoing request to the external component.
        /// </summary>
        Client = 2,
    }
}