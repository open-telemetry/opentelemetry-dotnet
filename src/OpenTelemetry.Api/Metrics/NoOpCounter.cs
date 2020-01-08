﻿// <copyright file="NoOpCounter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// A no-op counter instrument.
    /// </summary>
    /// <typeparam name="T">The type of counter. Only long and double are supported now.</typeparam>
    public sealed class NoOpCounter<T> : Counter<T>
        where T : struct
    {
        /// <summary>
        /// No op counter instance.
        /// </summary>
        public static readonly NoOpCounter<T> Instance = new NoOpCounter<T>();

        /// <inheritdoc/>
        public override CounterHandle<T> GetHandle(LabelSet labelset)
        {
            return NoOpCounterHandle<T>.Instance;
        }

        /// <inheritdoc/>
        public override CounterHandle<T> GetHandle(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return NoOpCounterHandle<T>.Instance;
        }
    }
}
