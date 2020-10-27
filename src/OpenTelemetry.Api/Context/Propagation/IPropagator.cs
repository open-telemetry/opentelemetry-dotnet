// <copyright file="IPropagator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// Defines an interface for Propagator, which is used to read and write
    /// context data from and to message exchanges by the applications.
    /// </summary>
    public interface IPropagator
    {
        /// <summary>
        /// Gets the list of headers used by propagator. The use cases of this are:
        ///   * allow pre-allocation of fields, especially in systems like gRPC Metadata
        ///   * allow a single-pass over an iterator (ex OpenTracing has no getter in TextMap).
        /// </summary>
        ISet<string> Fields { get; }

        /// <summary>
        /// Injects the context into a carrier.
        /// </summary>
        /// <typeparam name="T">Type of an object to set context on. Typically HttpRequest or similar.</typeparam>
        /// <param name="context">The default context to transmit over the wire.</param>
        /// <param name="carrier">Object to set context on. Instance of this object will be passed to setter.</param>
        /// <param name="setter">Action that will set name and value pair on the object.</param>
        void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter);

        /// <summary>
        /// Extracts the context from a carrier.
        /// </summary>
        /// <typeparam name="T">Type of object to extract context from. Typically HttpRequest or similar.</typeparam>
        /// <param name="context">The default context to be used if Extract fails.</param>
        /// <param name="carrier">Object to extract context from. Instance of this object will be passed to the getter.</param>
        /// <param name="getter">Function that will return string value of a key with the specified name.</param>
        /// <returns>Context from it's text representation.</returns>
        PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter);
    }
}
