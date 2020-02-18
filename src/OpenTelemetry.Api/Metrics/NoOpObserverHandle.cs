// <copyright file="NoOpObserverHandle.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Context;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// No-Op observer handle.
    /// </summary>
    /// <typeparam name="T">The type of observer. Only long and double are supported now.</typeparam>
    public sealed class NoOpObserverHandle<T> : ObserverHandle<T>
        where T : struct
    {
        /// <summary>
        /// No op observer handle instance.
        /// </summary>
        public static readonly NoOpObserverHandle<T> Instance = new NoOpObserverHandle<T>();

        /// <inheritdoc/>
        public override void Observe(in SpanContext context, T value)
        {
        }

        /// <inheritdoc/>
        public override void Observe(in DistributedContext context, T value)
        {
        }
    }
}
