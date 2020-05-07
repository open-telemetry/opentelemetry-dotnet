// <copyright file="JaegerSpanRefType.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    /// <summary>
    /// Represents the different types of Jaeger Spans.
    /// </summary>
    internal enum JaegerSpanRefType
    {
        /// <summary>
        /// A child span
        /// </summary>
        CHILD_OF = 0,

        /// <summary>
        /// A sibling span
        /// </summary>
        FOLLOWS_FROM = 1,
    }
}
