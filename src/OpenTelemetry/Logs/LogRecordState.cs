// <copyright file="LogRecordState.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Stores details about state attached to a log message.
    /// </summary>
    public readonly ref struct LogRecordState
    {
        private readonly IReadOnlyList<KeyValuePair<string, object>> stateValues;

        internal LogRecordState(object state, IReadOnlyList<KeyValuePair<string, object>> stateValues)
        {
            this.State = state;
            this.stateValues = stateValues;
        }

        /// <summary>
        /// Gets the raw state value.
        /// </summary>
        public object State { get; }

        /// <summary>
        /// Gets an <see cref="Enumerator"/> for looping over the inner values
        /// of the state. Only available when <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> is <see
        /// langword="true"/>.
        /// </summary>
        /// <returns><see cref="Enumerator"/>.</returns>
        public Enumerator GetEnumerator() => new(this.stateValues);

        /// <summary>
        /// LogRecordState enumerator.
        /// </summary>
        public struct Enumerator
        {
            private static readonly IReadOnlyList<KeyValuePair<string, object>> Empty = Array.Empty<KeyValuePair<string, object>>();
            private readonly IReadOnlyList<KeyValuePair<string, object>> stateValues;
            private int position;

            internal Enumerator(IReadOnlyList<KeyValuePair<string, object>> stateValues)
            {
                this.position = 0;
                this.stateValues = stateValues ?? Empty;
                this.Current = default;
            }

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public KeyValuePair<string, object> Current { get; private set; }

            /// <inheritdoc cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                if (this.position < this.stateValues.Count)
                {
                    this.Current = this.stateValues[this.position++];
                    return true;
                }

                return false;
            }
        }
    }
}
