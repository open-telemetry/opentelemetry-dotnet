// <copyright file="Event.cs" company="OpenTelemetry Authors">
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using OpenTelemetry.Api.Utils;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A text annotation associated with a collection of attributes.
    /// </summary>
    public sealed class Event
    {
        internal ActivityEvent ActivityEvent;
        private static readonly ReadOnlyDictionary<string, object> EmptyAttributes =
                new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        public Event(string name)
        {
            this.ActivityEvent = new ActivityEvent(name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="timestamp">Event timestamp. Timestamp MUST only be used for the events that happened in the past, not at the moment of this call.</param>
        public Event(string name, DateTimeOffset timestamp)
        {
            this.ActivityEvent = new ActivityEvent(name, timestamp);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="timestamp">Event timestamp. Timestamp MUST only be used for the events that happened in the past, not at the moment of this call.</param>
        /// <param name="attributes">Event attributes.</param>
        public Event(string name, DateTimeOffset timestamp, IDictionary<string, object> attributes)
        {
            this.ActivityEvent = new ActivityEvent(name, timestamp, attributes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="attributes">Event attributes.</param>
        public Event(string name, IDictionary<string, object> attributes)
        {
            this.ActivityEvent = new ActivityEvent(name, attributes);
        }

        /// <summary>
        /// Gets the <see cref="Event"/> name.
        /// </summary>
        public string Name
        {
            get
            {
                return this.ActivityEvent.Name;
            }
        }

        /// <summary>
        /// Gets the <see cref="Event"/> timestamp.
        /// </summary>
        public DateTimeOffset Timestamp
        {
            get
            {
                return this.ActivityEvent.Timestamp;
            }
        }

        /// <summary>
        /// Gets the collection of attributes associated with the event.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes
        {
            get
            {
                return this.ActivityEvent.Attributes;
            }
        }
    }
}
