// <copyright file="CorrelationContextBuilder.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Correlation context Builder.
    /// </summary>
    public struct CorrelationContextBuilder : System.IEquatable<CorrelationContextBuilder>
    {
        private List<CorrelationContextEntry> entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationContextBuilder"/> struct.
        /// </summary>
        /// <param name="inheritCurrentContext">Flag to allow inheriting the current context entries.</param>
        public CorrelationContextBuilder(bool inheritCurrentContext)
        {
            this.entries = null;

            if (DistributedContext.Carrier is NoopDistributedContextCarrier)
            {
                return;
            }

            if (inheritCurrentContext)
            {
                this.entries = new List<CorrelationContextEntry>(DistributedContext.Current.CorrelationContext.Entries);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationContextBuilder"/> struct using some context.
        /// </summary>
        /// <param name="context">Initial context.</param>
        public CorrelationContextBuilder(CorrelationContext context)
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier)
            {
                this.entries = null;
                return;
            }

            this.entries = new List<CorrelationContextEntry>(context.Entries);
        }

        /// <summary>
        /// Compare two entries of <see cref="CorrelationContextBuilder"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator ==(CorrelationContextBuilder left, CorrelationContextBuilder right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compare two entries of <see cref="CorrelationContextBuilder"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator !=(CorrelationContextBuilder left, CorrelationContextBuilder right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Create <see cref="CorrelationContext"/> instance from key and value entry.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        /// <returns>Instance of <see cref="CorrelationContext"/>.</returns>
        public static CorrelationContext CreateContext(string key, string value) =>
            new CorrelationContextBuilder(inheritCurrentContext: false).Add(key, value).Build();

        /// <summary>
        /// Create <see cref="CorrelationContext"/> instance from entry.
        /// </summary>
        /// <param name="entry">Entry to add to the context.</param>
        /// <returns>Instance of <see cref="CorrelationContext"/>.</returns>
        public static CorrelationContext CreateContext(CorrelationContextEntry entry) =>
            new CorrelationContextBuilder(inheritCurrentContext: false).Add(entry).Build();

        /// <summary>
        /// Create <see cref="CorrelationContext"/> instance from entry.
        /// </summary>
        /// <param name="entries">List of entries to add to the context.</param>
        /// <returns>Instance of <see cref="CorrelationContext"/>.</returns>
        public static CorrelationContext CreateContext(IEnumerable<CorrelationContextEntry> entries) =>
            new CorrelationContextBuilder(inheritCurrentContext: false).Add(entries).Build();

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="entry">Entry to add to the context.</param>
        /// <returns>The current <see cref="CorrelationContextBuilder"/> instance.</returns>
        public CorrelationContextBuilder Add(CorrelationContextEntry entry)
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier || entry == default)
            {
                return this;
            }

            if (this.entries == null)
            {
                this.entries = new List<CorrelationContextEntry>();
            }
            else
            {
                for (int i = 0; i < this.entries.Count; i++)
                {
                    if (this.entries[i].Key == entry.Key)
                    {
                        this.entries[i] = entry;
                        return this;
                    }
                }
            }

            this.entries.Add(entry);
            return this;
        }

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        /// <param name="metadata">Entry metadata.</param>
        /// <returns>The current <see cref="CorrelationContextBuilder"/> instance.</returns>
        public CorrelationContextBuilder Add(string key, string value, EntryMetadata metadata)
        {
            return this.Add(new CorrelationContextEntry(key, value, metadata));
        }

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        /// <returns>The current <see cref="CorrelationContextBuilder"/> instance.</returns>
        public CorrelationContextBuilder Add(string key, string value)
        {
            return this.Add(new CorrelationContextEntry(key, value));
        }

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="entries">List of entries to add to the context.</param>
        /// <returns>The current <see cref="CorrelationContextBuilder"/> instance.</returns>
        public CorrelationContextBuilder Add(IEnumerable<CorrelationContextEntry> entries)
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier || entries == null)
            {
                return this;
            }

            foreach (var entry in entries)
            {
                this.Add(entry);
            }

            return this;
        }

        /// <summary>
        /// Remove Distributed Context entry from the context.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>The current <see cref="CorrelationContextBuilder"/> instance.</returns>
        public CorrelationContextBuilder Remove(string key)
        {
            if (key == null || DistributedContext.Carrier is NoopDistributedContextCarrier || this.entries == null)
            {
                return this;
            }

            int index = this.entries.FindIndex(entry => entry.Key == key);
            if (index >= 0)
            {
                this.entries.RemoveAt(index);
            }

            return this;
        }

        /// <summary>
        /// Build a Correlation Context from current builder.
        /// </summary>
        /// <returns><see cref="CorrelationContext"/> instance.</returns>
        public CorrelationContext Build()
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier || this.entries == null)
            {
                return CorrelationContext.Empty;
            }

            var context = new CorrelationContext(this.entries);
            this.entries = null; // empty current builder entries.
            return context;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CorrelationContextBuilder builder &&
                   EqualityComparer<List<CorrelationContextEntry>>.Default.Equals(this.entries, builder.entries);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.entries.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CorrelationContextBuilder other)
        {
            return EqualityComparer<List<CorrelationContextEntry>>.Default.Equals(this.entries, other.entries);
        }
    }
}
