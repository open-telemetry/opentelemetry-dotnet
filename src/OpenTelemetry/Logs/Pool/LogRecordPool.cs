// <copyright file="LogRecordPool.cs" company="OpenTelemetry Authors">
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

#nullable enable

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Manages a pool of <see cref="LogRecord"/> instances.
    /// </summary>
    public static class LogRecordPool
    {
        internal const int DefaultMaxNumberOfAttributes = 64;
        internal const int DefaultMaxNumberOfScopes = 16;

        /// <summary>
        /// Resize the pool.
        /// </summary>
        /// <param name="capacity">The maximum number of <see cref="LogRecord"/>s to store in the pool.</param>
        public static void Resize(int capacity)
        {
            Guard.ThrowIfOutOfRange(capacity, min: 1);

            LogRecordSharedPool.Current = new(capacity);
        }

        internal static void Clear(LogRecord logRecord)
        {
            var attributeStorage = logRecord.AttributeStorage;
            if (attributeStorage != null)
            {
                if (attributeStorage.Count > DefaultMaxNumberOfAttributes)
                {
                    // Don't allow the pool to grow unconstained.
                    logRecord.AttributeStorage = null;
                }
                else
                {
                    /* List<T>.Clear sets the size to 0 but it maintains the
                    underlying array. */
                    attributeStorage.Clear();
                }
            }

            var bufferedScopes = logRecord.BufferedScopes;
            if (bufferedScopes != null)
            {
                if (bufferedScopes.Count > DefaultMaxNumberOfScopes)
                {
                    // Don't allow the pool to grow unconstained.
                    logRecord.BufferedScopes = null;
                }
                else
                {
                    /* List<T>.Clear sets the size to 0 but it maintains the
                    underlying array. */
                    bufferedScopes.Clear();
                }
            }
        }
    }
}
