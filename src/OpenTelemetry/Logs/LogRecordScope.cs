// <copyright file="LogRecordScope.cs" company="OpenTelemetry Authors">
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

#if NET461 || NETSTANDARD2_0
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Stores details about a scope attached to a log message.
    /// </summary>
    public readonly struct LogRecordScope
    {
        internal LogRecordScope(object scope)
        {
            this.Scope = scope;
        }

        /// <summary>
        /// Gets the raw scope value.
        /// </summary>
        public object Scope { get; }

        /// <summary>
        /// Gets an <see cref="IEnumerator"/> for looping over the inner values
        /// of the scope.
        /// </summary>
        /// <returns><see cref="Enumerator"/>.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this.Scope);

        /// <summary>
        /// LogRecordScope enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly IReadOnlyList<KeyValuePair<string, object>> scope;
            private int position;

            public Enumerator(object scope)
            {
                if (scope is IReadOnlyList<KeyValuePair<string, object>> scopeList)
                {
                    this.scope = scopeList;
                }
                else if (scope is IEnumerable<KeyValuePair<string, object>> scopeEnumerable)
                {
                    this.scope = new List<KeyValuePair<string, object>>(scopeEnumerable);
                }
                else
                {
                    this.scope = new List<KeyValuePair<string, object>>
                    {
                        new KeyValuePair<string, object>(string.Empty, scope),
                    };
                }

                this.position = 0;
                this.Current = default;
            }

            public KeyValuePair<string, object> Current { get; private set; }

            object IEnumerator.Current => this.Current;

            public bool MoveNext()
            {
                if (this.position < this.scope.Count)
                {
                    this.Current = this.scope[this.position++];
                    return true;
                }

                return false;
            }

            public void Dispose()
            {
            }

            public void Reset()
                => throw new NotSupportedException();
        }
    }
}
#endif
