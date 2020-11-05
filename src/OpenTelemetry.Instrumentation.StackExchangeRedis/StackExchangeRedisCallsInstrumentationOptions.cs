// <copyright file="StackExchangeRedisCallsInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.StackExchangeRedis
{
    /// <summary>
    /// Options for StackExchange.Redis instrumentation.
    /// </summary>
    public class StackExchangeRedisCallsInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets the maximum time that should elapse between flushing the internal buffer of Redis profiling sessions and creating <see cref="Activity"/> objects. Default value: 00:00:10.
        /// </summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(10);
    }
}
