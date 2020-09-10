// <copyright file="ThreadLocalRuntimeContextSlot.cs" company="OpenTelemetry Authors">
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

using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// The thread local (TLS) implementation of context slot.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    public class ThreadLocalRuntimeContextSlot<T> : RuntimeContextSlot<T>
    {
        private readonly ThreadLocal<T> slot;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadLocalRuntimeContextSlot{T}"/> class.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        public ThreadLocalRuntimeContextSlot(string name)
            : base(name)
        {
            this.slot = new ThreadLocal<T>();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T Get()
        {
            return this.slot.Value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Set(T value)
        {
            this.slot.Value = value;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(true);
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.slot.Dispose();
                }

                this.disposedValue = true;
            }
        }
    }
}
