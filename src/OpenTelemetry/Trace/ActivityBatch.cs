// <copyright file="ActivityBatch.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Stores a batch of completed <see cref="Activity"/> objects to be exported.
    /// </summary>
    public readonly struct ActivityBatch
    {
        private readonly Activity activity;
        private readonly CircularBuffer<Activity> circularBuffer;

        internal ActivityBatch(Activity activity)
        {
            this.activity = activity ?? throw new ArgumentNullException(nameof(activity));
            this.circularBuffer = null;
        }

        internal ActivityBatch(CircularBuffer<Activity> circularBuffer)
        {
            this.activity = null;
            this.circularBuffer = circularBuffer ?? throw new ArgumentNullException(nameof(circularBuffer));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="ActivityBatch"/>.
        /// </summary>
        /// <returns><see cref="ActivityEnumerator"/>.</returns>
        public ActivityEnumerator GetEnumerator()
        {
            return this.circularBuffer != null
                ? new ActivityEnumerator(this.circularBuffer)
                : new ActivityEnumerator(this.activity);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="ActivityBatch"/>.
        /// </summary>
        public struct ActivityEnumerator : IEnumerator<Activity>
        {
            private readonly CircularBuffer<Activity> circularBuffer;
            private int count;

            internal ActivityEnumerator(Activity activity)
            {
                this.Current = activity;
                this.circularBuffer = null;
                this.count = -1;
            }

            internal ActivityEnumerator(CircularBuffer<Activity> circularBuffer)
            {
                this.Current = null;
                this.circularBuffer = circularBuffer;
                this.count = circularBuffer.Count;
            }

            /// <inheritdoc/>
            public Activity Current { get; private set; }

            /// <inheritdoc/>
            object IEnumerator.Current => this.Current;

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                var circularBuffer = this.circularBuffer;

                if (circularBuffer == null)
                {
                    if (this.count >= 0)
                    {
                        this.Current = null;
                        return false;
                    }

                    this.count++;
                    return true;
                }

                if (this.count > 0)
                {
                    this.Current = circularBuffer.Read();
                    this.count--;
                    return true;
                }

                this.Current = null;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
                => throw new NotSupportedException();
        }
    }
}
