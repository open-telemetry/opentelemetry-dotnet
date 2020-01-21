// <copyright file="DistributedContextBuilder.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Distributed context Builder.
    /// </summary>
    public struct DistributedContextBuilder
    {
        private List<DistributedContextEntry> entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContextBuilder"/> struct.
        /// </summary>
        /// <param name="inheritCurrentContext">Flag to allow inheriting the current context entries.</param>
        public DistributedContextBuilder(bool inheritCurrentContext)
        {
            this.entries = null;

            if (DistributedContext.Carrier is NoopDistributedContextCarrier)
            {
                return;
            }

            if (inheritCurrentContext)
            {
                this.entries = new List<DistributedContextEntry>(DistributedContext.Current.Entries);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContextBuilder"/> struct using some context.
        /// </summary>
        /// <param name="context">Initial context.</param>
        public DistributedContextBuilder(DistributedContext context)
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier)
            {
                this.entries = null;
                return;
            }

            this.entries = new List<DistributedContextEntry>(context.Entries);
        }

        /// <summary>
        /// Create <see cref="DistributedContext"/> instance from key and value entry.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        /// <returns>Instance of <see cref="DistributedContext"/>.</returns>
        public static DistributedContext CreateContext(string key, string value) =>
            new DistributedContextBuilder(inheritCurrentContext: false).Add(key, value).Build();

        /// <summary>
        /// Create <see cref="DistributedContext"/> instance from entry.
        /// </summary>
        /// <param name="entry">Entry to add to the context.</param>
        /// <returns>Instance of <see cref="DistributedContext"/>.</returns>
        public static DistributedContext CreateContext(DistributedContextEntry entry) =>
            new DistributedContextBuilder(inheritCurrentContext: false).Add(entry).Build();

        /// <summary>
        /// Create <see cref="DistributedContext"/> instance from entry.
        /// </summary>
        /// <param name="entries">List of entries to add to the context.</param>
        /// <returns>Instance of <see cref="DistributedContext"/>.</returns>
        public static DistributedContext CreateContext(IEnumerable<DistributedContextEntry> entries) =>
            new DistributedContextBuilder(inheritCurrentContext: false).Add(entries).Build();

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="entry">Entry to add to the context.</param>
        /// <returns>The current <see cref="DistributedContextBuilder"/> instance.</returns>
        public DistributedContextBuilder Add(DistributedContextEntry entry)
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier || entry == default)
            {
                return this;
            }

            if (this.entries == null)
            {
                this.entries = new List<DistributedContextEntry>();
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
        /// <returns>The current <see cref="DistributedContextBuilder"/> instance.</returns>
        public DistributedContextBuilder Add(string key, string value, EntryMetadata metadata)
        {
            return this.Add(new DistributedContextEntry(key, value, metadata));
        }

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        /// <returns>The current <see cref="DistributedContextBuilder"/> instance.</returns>
        public DistributedContextBuilder Add(string key, string value)
        {
            return this.Add(new DistributedContextEntry(key, value));
        }

        /// <summary>
        /// Add Distributed Context entry to the builder.
        /// </summary>
        /// <param name="entries">List of entries to add to the context.</param>
        /// <returns>The current <see cref="DistributedContextBuilder"/> instance.</returns>
        public DistributedContextBuilder Add(IEnumerable<DistributedContextEntry> entries)
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier || entries == null)
            {
                return this;
            }

            foreach (DistributedContextEntry entry in entries)
            {
                this.Add(entry);
            }

            return this;
        }

        /// <summary>
        /// Remove Distributed Context entry from the context.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>The current <see cref="DistributedContextBuilder"/> instance.</returns>
        public DistributedContextBuilder Remove(string key)
        {
            if (key == null || DistributedContext.Carrier is NoopDistributedContextCarrier || this.entries == null)
            {
                return this;
            }

            int index = this.entries.FindIndex((DistributedContextEntry entry) => entry.Key == key);
            if (index >= 0)
            {
                this.entries.RemoveAt(index);
            }

            return this;
        }

        /// <summary>
        /// Build a Distributed Context from current builder.
        /// </summary>
        /// <returns><see cref="DistributedContext"/> instance.</returns>
        public DistributedContext Build()
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier || this.entries == null)
            {
                return DistributedContext.Empty;
            }

            DistributedContext context = new DistributedContext(this.entries);
            this.entries = null; // empty current builder entries.
            return context;
        }
    }
}
