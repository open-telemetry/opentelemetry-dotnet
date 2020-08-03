// <copyright file="SuppressInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context;

namespace OpenTelemetry
{
    public class SuppressInstrumentation : IDisposable
    {
        private static readonly RuntimeContextSlot<SuppressInstrumentation> SuppressInstrumentationRuntimeContextSlot = RuntimeContext.RegisterSlot<SuppressInstrumentation>("otel.suppress_instrumentation");

        private readonly SuppressInstrumentation parent;
        private bool disposed;

        private SuppressInstrumentation()
        {
            this.parent = SuppressInstrumentationRuntimeContextSlot.Get();
            SuppressInstrumentationRuntimeContextSlot.Set(this);
        }

        /// <summary>
        /// Gets a value indicating whether automatic telemetry
        /// collection in the current context should be suppressed (disabled).
        /// </summary>
        public static bool IsSuppressed => SuppressInstrumentationRuntimeContextSlot.Get() != default;

        /// <summary>
        /// Begins a new scope in which automatic telemetry is suppressed (disabled).
        /// </summary>
        /// <returns>Object to dispose to end the scope.</returns>
        /// <remarks>
        /// This is typically used to prevent infinite loops created by
        /// collection of internal operations, such as exporting traces over HTTP.
        /// <code>
        ///    public override async Task&lt;ExportResult&gt; ExportAsync(
        ///        IEnumerable&lt;Activity&gt; batch,
        ///        CancellationToken cancellationToken)
        ///    {
        ///       using (var scope = SuppressInstrumentation.Begin())
        ///       {
        ///           // Instrumentation is suppressed (i.e., SuppressInstrumentation.IsSuppressed == true)
        ///       }
        ///
        ///       // Instrumentation is not suppressed (i.e., SuppressInstrumentation.IsSuppressed == false)
        ///    }
        /// </code>
        /// </remarks>
        public static IDisposable Begin()
        {
            return new SuppressInstrumentation();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                SuppressInstrumentationRuntimeContextSlot.Set(this.parent);
                this.disposed = true;
            }
        }
    }
}
