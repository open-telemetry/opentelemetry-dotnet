// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using StackExchange.Redis;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Enables the outgoing requests automatic data collection for Redis.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="connection"><see cref="ConnectionMultiplexer"/> to instrument.</param>
        /// <param name="configureOptions">Redis configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddRedisInstrumentation(
            this TracerProviderBuilder builder,
            ConnectionMultiplexer connection,
            Action<StackExchangeRedisCallsInstrumentationOptions> configureOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            StackExchangeRedisCallsInstrumentationOptions options = new StackExchangeRedisCallsInstrumentationOptions();
            configureOptions?.Invoke(options);

            return builder
                .AddInstrumentation((activitySourceAdapter) => new StackExchangeRedisCallsInstrumentation(connection, options))
                .AddSource(StackExchangeRedisCallsInstrumentation.ActivitySourceName);
        }
    }
}
