// <copyright file="SpanAttributes.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A class that represents the span attributes. Read more here https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/common/common.md#attributes.
    /// </summary>
    /// <remarks>SpanAttributes is a wrapper around <see cref="ActivityTagsCollection"/> class.</remarks>
    public class SpanAttributes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanAttributes"/> class.
        /// </summary>
        public SpanAttributes()
        {
            this.Attributes = new ActivityTagsCollection();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanAttributes"/> class.
        /// </summary>
        /// <param name="attributes">Initial attributes to store in the collection.</param>
        public SpanAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
            : this()
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            foreach (KeyValuePair<string, object> kvp in attributes)
            {
                this.AddInternal(kvp.Key, kvp.Value);
            }
        }

        internal ActivityTagsCollection Attributes { get; }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, long value)
        {
            this.AddInternal(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, string value)
        {
            this.AddInternal(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, bool value)
        {
            this.AddInternal(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, double value)
        {
            this.AddInternal(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, long[] values)
        {
            this.AddInternal(key, values);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, string[] values)
        {
            this.AddInternal(key, values);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, bool[] values)
        {
            this.AddInternal(key, values);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, double[] values)
        {
            this.AddInternal(key, values);
        }

        private void AddInternal(string key, object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(key);
            }

            this.Attributes[key] = value;
        }
    }
}
