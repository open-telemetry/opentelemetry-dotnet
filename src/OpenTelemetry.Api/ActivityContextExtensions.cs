// <copyright file="ActivityContextExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Runtime.CompilerServices;

// The ActivityContext class is in the System.Diagnostics namespace.
// These extension methods on ActivityContext are intentionally not placed in the
// same namespace as Activity to prevent name collisions in the future.
// The OpenTelemetry namespace is used because ActivityContext applies to all types
// of telemetry data - i.e. traces, metrics, and logs.
namespace OpenTelemetry
{
    /// <summary>
    /// Extension methods on ActivityContext.
    /// </summary>
    public static class ActivityContextExtensions
    {
        /// <summary>
        /// Returns a value indicating whether or not a <see cref="ActivityContext"/> is valid.
        /// </summary>
        /// <param name="ctx"><see cref="ActivityContext"/>.</param>
        /// <returns><see langword="true"/> if the context is a valid otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use the IsValid method which accepts ActivityContext by reference for better performance.")]
        public static bool IsValid(this ActivityContext ctx)
        {
            return ctx != default;
        }

        /// <summary>
        /// Returns a value indicating whether or not a <see cref="ActivityContext"/> is valid.
        /// </summary>
        /// <param name="ctx"><see cref="ActivityContext"/>.</param>
        /// <returns><see langword="true"/> if the context is a valid otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this in ActivityContext ctx)
        {
            return !ctx.SpanId.Equals(default)
                && !ctx.TraceId.Equals(default);
        }
    }
}
