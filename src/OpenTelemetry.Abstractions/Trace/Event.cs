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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using OpenTelemetry.Abstractions.Utils;

    /// <inheritdoc/>
    public sealed class Event : IEvent
    {
        private static readonly ReadOnlyDictionary<string, object> EmptyAttributes =
                new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        internal Event(string name, DateTimeOffset timestamp, IDictionary<string, object> attributes)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            this.Timestamp = timestamp != default ? timestamp : PreciseTimestamp.GetUtcNow();
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc/>
        public IDictionary<string, object> Attributes { get; }

        /// <summary>
        /// Returns a new <see cref="Event"/> with the provided name.
        /// </summary>
        /// <param name="name">The text name for the <see cref="Event"/>.</param>
        /// <returns>A new <see cref="Event"/> with the provided name.</returns>
        /// <exception cref="ArgumentNullException">If <c>name</c> is <c>null</c>.</exception>
        public static IEvent Create(string name)
        {
            return new Event(name, default, EmptyAttributes);
        }

        /// <summary>
        /// Returns a new <see cref="Event"/> with the provided name.
        /// </summary>
        /// <param name="name">The text name for the <see cref="Event"/>.</param>
        /// <param name="timestamp">The timestamp of the <see cref="Event"/>.</param>
        /// <returns>A new <see cref="Event"/> with the provided name.</returns>
        /// <exception cref="ArgumentNullException">If <c>name</c> is <c>null</c>.</exception>
        public static IEvent Create(string name, DateTimeOffset timestamp)
        {
            return new Event(name, timestamp, EmptyAttributes);
        }

        /// <summary>
        /// Returns a new <see cref="Event"/> with the provided name and set of attributes.
        /// </summary>
        /// <param name="name">The text name for the <see cref="Event"/>.</param>
        /// <param name="timestamp">The timestamp of the <see cref="Event"/>.</param>
        /// <param name="attributes">The <see cref="IDictionary{String, Object}"/> of attributes for the <see cref="Event"/>.</param>
        /// <returns>A new <see cref="Event"/> with the provided name and set of attributes.</returns>
        /// <exception cref="ArgumentNullException">If <c>name</c> or <c>attributes</c> is <c>null</c>.</exception>
        public static IEvent Create(string name, DateTimeOffset timestamp, IDictionary<string, object> attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            IDictionary<string, object> readOnly = new ReadOnlyDictionary<string, object>(attributes);
            return new Event(name, timestamp, readOnly);
        }

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

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(Event)
                + "{"
                + nameof(this.Name) + "=" + this.Name + ", "
                + nameof(this.Attributes) + "=" + string.Join(", ", this.Attributes.Select(kvp => (kvp.Key + "=" + kvp.Value))) + ", "
                + nameof(this.Timestamp) + "=" + this.Timestamp.ToString("o")
                + "}";
        }
    }
}
