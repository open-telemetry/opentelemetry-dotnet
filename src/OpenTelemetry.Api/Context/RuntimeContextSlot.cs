// <copyright file="RuntimeContextSlot.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context
{
    /// <summary>
    /// The abstract context slot.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    public abstract class RuntimeContextSlot<T> : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeContextSlot{T}"/> class.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        protected RuntimeContextSlot(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of the context slot.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Get the value from the context slot.
        /// </summary>
        /// <returns>The value retrieved from the context slot.</returns>
        public abstract T Get();

        /// <summary>
        /// Set the value to the context slot.
        /// </summary>
        /// <param name="value">The value to be set.</param>
        public abstract void Set(T value);

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
