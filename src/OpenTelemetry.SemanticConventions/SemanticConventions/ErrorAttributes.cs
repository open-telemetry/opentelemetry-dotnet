// <copyright file="ErrorAttributes.cs" company="OpenTelemetry Authors">
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

// <auto-generated> This file has been auto generated from buildscripts/semantic-conventions/templates/Attributes.cs.j2</auto-generated>

using System;

namespace OpenTelemetry.SemanticConventions
{
    /// <summary>
    /// Describes semantic conventions for attributes in the <c>error</c> namespace.
    /// </summary>
    public static class ErrorAttributes
    {
        /// <summary>
        /// Describes a class of error the operation ended with.
        /// </summary>
        /// <remarks>
        /// The <c>error.type</c> SHOULD be predictable and SHOULD have low cardinality.
        /// Instrumentations SHOULD document the list of errors they report.The cardinality of <c>error.type</c> within one instrumentation library SHOULD be low.
        /// Telemetry consumers that aggregate data from multiple instrumentation libraries and applications
        /// should be prepared for <c>error.type</c> to have high cardinality at query time when no
        /// additional filters are applied.If the operation has completed successfully, instrumentations SHOULD NOT set <c>error.type</c>.If a specific domain defines its own set of error identifiers (such as HTTP or gRPC status codes),
        /// it&amp;#39;s RECOMMENDED to:<ul>
        /// <li>Use a domain-specific attribute</li>
        /// <li>Set <c>error.type</c> to capture all errors, regardless of whether they are defined within the domain-specific set or not</li>
        /// </ul>.
        /// </remarks>
        public const string ErrorType = "error.type";

        /// <summary>
        /// Describes a class of error the operation ended with.
        /// </summary>
        public static class ErrorTypeValues
        {
            /// <summary>
            /// A fallback error value to be used when the instrumentation doesn't define a custom value.
            /// </summary>
            public const string Other = "_OTHER";
        }
    }
}