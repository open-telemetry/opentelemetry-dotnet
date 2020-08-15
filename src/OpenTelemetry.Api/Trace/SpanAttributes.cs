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
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A class that represents the span attributes. Read more here https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/common/common.md#attributes.
    /// </summary>
    public class SpanAttributes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanAttributes"/> class.
        /// </summary>
        public SpanAttributes()
        {
            this.Attributes = new ActivityTagsCollection();
        }

        internal ActivityTagsCollection Attributes { get; }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, long value)
        {
            this.PrivateAdd(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, string value)
        {
            this.PrivateAdd(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, bool value)
        {
            this.PrivateAdd(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="value">Entry value.</param>
        public void Add(string key, double value)
        {
            this.PrivateAdd(key, value);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, long[] values)
        {
            this.PrivateAdd(key, values);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, string[] values)
        {
            this.PrivateAdd(key, values);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, bool[] values)
        {
            this.PrivateAdd(key, values);
        }

        /// <summary>
        /// Add entry to the attributes.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="values">Entry value.</param>
        public void Add(string key, double[] values)
        {
            this.PrivateAdd(key, values);
        }

        private void PrivateAdd(string key, object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(key);
            }

            this.Attributes[key] = value;
        }
    }
}
