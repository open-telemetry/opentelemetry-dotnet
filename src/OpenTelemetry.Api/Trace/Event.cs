﻿// <copyright file="Event.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenTelemetry.Api.Utils;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A text annotation associated with a collection of attributes.
    /// </summary>
    public sealed class Event
    {
        private static readonly ReadOnlyDictionary<string, object> EmptyAttributes =
                new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        public Event(string name)
            : this(name, PreciseTimestamp.GetUtcNow(), EmptyAttributes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="timestamp">Event timestamp. Timestamp MUST only be used for the events that happened in the past, not at the moment of this call.</param>
        public Event(string name, DateTimeOffset timestamp)
            : this(name, timestamp, EmptyAttributes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="timestamp">Event timestamp. Timestamp MUST only be used for the events that happened in the past, not at the moment of this call.</param>
        /// <param name="attributes">Event attributes.</param>
        public Event(string name, DateTimeOffset timestamp, IDictionary<string, object> attributes)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            this.Timestamp = timestamp != default ? timestamp : PreciseTimestamp.GetUtcNow();
        }

        /// <summary>
        /// Gets the <see cref="Event"/> name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the <see cref="Event"/> timestamp.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the <see cref="IDictionary{String, Object}"/> collection of attributes associated with the event.
        /// </summary>
        public IDictionary<string, object> Attributes { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (obj is Event @event)
            {
                return this.Name.Equals(@event.Name) &&
                    this.Attributes.SequenceEqual(@event.Attributes) &&
                    this.Timestamp.Equals(@event.Timestamp);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Name.GetHashCode();
            h *= 1000003;
            h ^= this.Attributes.GetHashCode();
            h *= 1000003;
            h ^= this.Timestamp.GetHashCode();
            return h;
        }
    }
}
