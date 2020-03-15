// <copyright file="CommonUtils.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Stackdriver.Utils
{
    /// <summary>
    /// Common Utility Methods that are not metrics/trace specific.
    /// </summary>
    public static class CommonUtils
    {
        /// <summary>
        /// Divide the source list into batches of lists of given size.
        /// </summary>
        /// <typeparam name="T">The type of the list.</typeparam>
        /// <param name="source">The list.</param>
        /// <param name="size">Size of the batch.</param>
        /// <returns><see cref="IEnumerable{T}"/>.</returns>
        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> source, int size)
        {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return WalkPartition(enumerator, size - 1);
            }
        }

        private static IEnumerable<T> WalkPartition<T>(IEnumerator<T> source, int size)
        {
            yield return source.Current;
            for (var i = 0; i < size && source.MoveNext(); i++)
            {
                yield return source.Current;
            }
        }
    }
}
