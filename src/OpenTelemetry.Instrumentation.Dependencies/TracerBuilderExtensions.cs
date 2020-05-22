// <copyright file="TracerBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Dependencies;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Extension methods to simplify registering of data collection.
    /// </summary>
    public static class TracerBuilderExtensions
    {
        /// <summary>
        /// Enables the outgoing requests automatic data collection.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder AddDependencyInstrumentation(this TracerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder
                .AddInstrumentation((t) => new AzureClientsInstrumentation(t))
                .AddInstrumentation((t) => new AzurePipelineInstrumentation(t))
                .AddInstrumentation((t) => new HttpClientInstrumentation(t))
                .AddInstrumentation((t) => new SqlClientInstrumentation(t));
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configureHttpInstrumentationOptions">Http configuration options.</param>
        /// <param name="configureSqlInstrumentationOptions">Sql configuration options.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder AddDependencyInstrumentation(
            this TracerBuilder builder,
            Action<HttpClientInstrumentationOptions> configureHttpInstrumentationOptions = null,
            Action<SqlClientInstrumentationOptions> configureSqlInstrumentationOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var httpOptions = new HttpClientInstrumentationOptions();
            configureHttpInstrumentationOptions?.Invoke(httpOptions);

            var sqlOptions = new SqlClientInstrumentationOptions();
            configureSqlInstrumentationOptions?.Invoke(sqlOptions);

            return builder
                .AddInstrumentation((t) => new AzureClientsInstrumentation(t))
                .AddInstrumentation((t) => new AzurePipelineInstrumentation(t))
                .AddInstrumentation((t) => new HttpClientInstrumentation(t, httpOptions))
                .AddInstrumentation((t) => new SqlClientInstrumentation(t, sqlOptions));
        }
    }
}
