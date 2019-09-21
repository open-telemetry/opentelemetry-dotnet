// <copyright file="IEvent.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A text annotation associated with a collection of attributes.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Gets the <see cref="IEvent"/> name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the <see cref="IEvent"/> timestamp.
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the <see cref="IDictionary{String, IAttributeValue}"/> collection of attributes associated with the event.
        /// </summary>
        IDictionary<string, object> Attributes { get; }
    }
}
