// <copyright file="IActivityEnumerator.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// An interface used to perform zero-allocation enumeration of <see cref="Activity"/> elements. Implementation must be a struct.
    /// </summary>
    /// <typeparam name="T">Enumerated item type.</typeparam>
    internal interface IActivityEnumerator<T>
    {
        /// <summary>
        /// Called for each <see cref="Activity"/> item while the enumeration is executing.
        /// </summary>
        /// <param name="item">Enumeration item.</param>
        /// <returns><see langword="true"/> to continue the enumeration of records or <see langword="false"/> to stop (break) the enumeration.</returns>
        bool ForEach(T item);
    }
}
