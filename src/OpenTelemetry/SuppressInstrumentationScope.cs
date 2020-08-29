﻿// <copyright file="SuppressInstrumentationScope.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using OpenTelemetry.Context;

namespace OpenTelemetry
{
    public sealed class SuppressInstrumentationScope : IDisposable
    {
        private static readonly RuntimeContextSlot<int> Slot = RuntimeContext.RegisterSlot<int>("otel.suppress_instrumentation");

        private readonly int previousValue;
        private bool disposed;

        internal SuppressInstrumentationScope(bool value = true)
        {
            this.previousValue = Slot.Get();
            Slot.Set(value ? -1 : 0);
        }

        internal static bool IsSuppressed => Slot.Get() != 0;

        /// <summary>
        /// Begins a new scope in which instrumentation is suppressed (disabled).
        /// </summary>
        /// <param name="value">Value indicating whether to suppress instrumentation.</param>
        /// <returns>Object to dispose to end the scope.</returns>
        /// <remarks>
        /// This is typically used to prevent infinite loops created by
        /// collection of internal operations, such as exporting traces over HTTP.
        /// <code>
        ///     public override async Task&lt;ExportResult&gt; ExportAsync(
        ///         IEnumerable&lt;Activity&gt; batch,
        ///         CancellationToken cancellationToken)
        ///     {
        ///         using (SuppressInstrumentationScope.Begin())
        ///         {
        ///             // Instrumentation is suppressed (i.e., Sdk.SuppressInstrumentation == true)
        ///         }
        ///
        ///         // Instrumentation is not suppressed (i.e., Sdk.SuppressInstrumentation == false)
        ///     }
        /// </code>
        /// </remarks>
        public static IDisposable Begin(bool value = true)
        {
            return new SuppressInstrumentationScope(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Enter()
        {
            var value = Slot.Get();

            if (value >= 0)
            {
                Slot.Set(++value);
            }

            return value;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                Slot.Set(this.previousValue);
                this.disposed = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TryEnter()
        {
            var value = Slot.Get();

            if (value > 0)
            {
                Slot.Set(++value);
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TryLeave()
        {
            var value = Slot.Get();

            if (value > 0)
            {
                Slot.Set(--value);
            }

            return value;
        }
    }
}
