// <copyright file="EventAttributes.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.SemanticConventions.v1_23_1_Experimental
{
    /// <summary>
    /// Describes semantic conventions for attributes in the <c>event</c> namespace.
    /// </summary>
    public static class EventAttributes
    {
        /// <summary>
        /// The domain identifies the business context for the events.
        /// </summary>
        /// <remarks>
        /// Events across different domains may have same <c>event.name</c>, yet be unrelated events.
        /// </remarks>
        public const string EventDomain = "event.domain";

        /// <summary>
        /// The name identifies the event.
        /// </summary>
        public const string EventName = "event.name";

        /// <summary>
        /// The domain identifies the business context for the events.
        /// </summary>
        public static class EventDomainValues
        {
            /// <summary>
            /// Events from browser apps.
            /// </summary>
            public const string Browser = "browser";
            /// <summary>
            /// Events from mobile apps.
            /// </summary>
            public const string Device = "device";
            /// <summary>
            /// Events from Kubernetes.
            /// </summary>
            public const string K8s = "k8s";
        }
    }
}