// <copyright file="TagsCollection.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;

namespace OpenTelemetry.Trace.Internal
{
    /// <summary>
    /// Tags collection.
    /// </summary>
    internal struct TagsCollection : IEnumerable<KeyValuePair<string, object>>
    {
        private IEnumerable<KeyValuePair<string, string>> enumerable;

        /// <summary>
        /// Initializes a new instance of the <see cref="TagsCollection"/> struct.
        /// </summary>
        /// <param name="tags">the tags.</param>
        public TagsCollection(IEnumerable<KeyValuePair<string, string>> tags)
        {
            this.enumerable = tags;
        }

        /// <inheritdoc/>
        public IEnumerator GetEnumerator() => new TagsEnumerator(this.enumerable.GetEnumerator());

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() => new TagsEnumerator(this.enumerable.GetEnumerator());

        private struct TagsEnumerator : IDisposable, IEnumerator<KeyValuePair<string, object>>
        {
            private IEnumerator<KeyValuePair<string, string>> enumerator;

            public TagsEnumerator(IEnumerator<KeyValuePair<string, string>> enumerator) => this.enumerator = enumerator;

            KeyValuePair<string, object> IEnumerator<KeyValuePair<string, object>>.Current => new KeyValuePair<string, object>(this.enumerator.Current.Key, (object)this.enumerator.Current.Value);

            object IEnumerator.Current => new KeyValuePair<string, object>(this.enumerator.Current.Key, (object)this.enumerator.Current.Value);

            public void Dispose() => this.enumerator.Dispose();

            public bool MoveNext() => this.enumerator.MoveNext();

            public void Reset() => this.enumerator.Reset();
        }
    }
}
