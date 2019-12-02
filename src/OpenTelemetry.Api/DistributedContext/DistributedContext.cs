// <copyright file="DistributedContext.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Distributed context.
    /// </summary>
    public readonly struct DistributedContext : IEquatable<DistributedContext>
    {
        private static DistributedContextCarrier carrier = NoopDistributedContextCarrier.Instance;
        private static List<DistributedContextEntry> emptyList = new List<DistributedContextEntry>();
        private readonly IEnumerable<DistributedContextEntry> entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContext"/> struct.
        /// </summary>
        /// <param name="entries">Entries for distributed context.</param>
        public DistributedContext(IEnumerable<DistributedContextEntry> entries)
        {
            if (entries is null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            foreach (DistributedContextEntry entry in entries)
            {
                if (entry == default)
                {
                    throw new ArgumentException($"'{nameof(entries)}' contains entries with null key and value");
                }
            }

            if (carrier is NoopDistributedContextCarrier || entries.Count() == 0)
            {
                this.entries = emptyList;
            }
            else
            {
                if (entries.Count() == 1)
                {
                    this.entries = new List<DistributedContextEntry>(entries);
                }
                else
                {
                    List<DistributedContextEntry> list = new List<DistributedContextEntry>();

                    // Remove the duplicates keys.
                    foreach (DistributedContextEntry entry in entries)
                    {
                        int i;
                        for (i = 0; i < list.Count; i++)
                        {
                            if (entry.Key == list[i].Key)
                            {
                                list[i] = entry;
                                break;
                            }
                        }

                        if (i >= list.Count)
                        {
                            list.Add(entry);
                        }
                    }

                    this.entries = list;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContext"/> struct.
        /// </summary>
        /// <param name="key">The key of the context entry.</param>
        /// <param name="value">The value of the context entry.</param>
        public DistributedContext(string key, string value)
        {
            if (key is null || value is null)
            {
                throw new ArgumentNullException(key is null ? nameof(key) : nameof(value));
            }

            this.entries = carrier is NoopDistributedContextCarrier ? emptyList : new List<DistributedContextEntry>(1) { new DistributedContextEntry(key, value) };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContext"/> struct.
        /// </summary>
        /// <param name="entry">The distributed context entry.</param>
        public DistributedContext(DistributedContextEntry entry)
        {
            if (entry.Key is null || entry.Value is null)
            {
                throw new ArgumentNullException(entry.Key is null ? nameof(entry.Key) : nameof(entry.Value));
            }

            this.entries = carrier is NoopDistributedContextCarrier ? emptyList : new List<DistributedContextEntry>(1) { entry };
        }

        /// <summary>
        /// Gets empty object of <see cref="DistributedContext"/> struct.
        /// </summary>
        public static DistributedContext Empty { get; } = new DistributedContext(emptyList);

        /// <summary>
        /// Gets the current <see cref="DistributedContext"/>.
        /// </summary>
        public static DistributedContext Current => carrier.Current;

        /// <summary>
        /// Gets or sets the default carrier instance of the <see cref="DistributedContextCarrier"/> class.
        /// SDK will need to override the value to AsyncLocalDistributedContextCarrier.Instance.
        /// </summary>
        public static DistributedContextCarrier Carrier
        {
            get => carrier;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                carrier = value;
            }
        }

        /// <summary>
        /// Gets all the <see cref="DistributedContextEntry"/> in this <see cref="DistributedContext"/>.
        /// </summary>
        public IEnumerable<DistributedContextEntry> Entries => this.entries;

        /// <summary>
        /// Sets the current <see cref="DistributedContext"/>.
        /// </summary>
        /// <param name="context">Context to set as current.</param>
        /// <returns>Scope object. On disposal - original context will be restored.</returns>
        public static IDisposable SetCurrent(in DistributedContext context) => carrier.SetCurrent(context);

        /// <summary>
        /// Gets the <see cref="DistributedContextEntry"/> with the specified name.
        /// </summary>
        /// <param name="key">Name of the <see cref="DistributedContextEntry"/> to get.</param>
        /// <returns>The <see cref="DistributedContextEntry"/> with the specified name. If not found - null.</returns>
        public string GetEntryValue(string key) => this.entries.FirstOrDefault(x => x.Key == key).Value;

        /// <inheritdoc/>
        public bool Equals(DistributedContext other)
        {
            if (this.entries.Count() != other.entries.Count())
            {
                return false;
            }

            foreach (DistributedContextEntry entry in this.entries)
            {
                if (other.GetEntryValue(entry.Key) != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
