// <copyright file="TracerBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using System;
using OpenTelemetry.Adapter.Dependencies;

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
        public static TracerBuilder AddDependencyAdapter(this TracerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder
                .AddAdapter((t) => new AzureClientsAdapter(t))
                .AddAdapter((t) => new AzurePipelineAdapter(t))
                .AddAdapter((t) => new HttpClientAdapter(t))
                .AddAdapter((t) => new HttpWebRequestAdapter(t))
                .AddAdapter((t) => new SqlClientAdapter(t));
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection.
        /// </summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configureHttpAdapterOptions">Http configuration options.</param>
        /// <param name="configureSqlAdapterOptions">Sql configuration options.</param>
        /// <returns>The instance of <see cref="TracerBuilder"/> to chain the calls.</returns>
        public static TracerBuilder AddDependencyAdapter(
            this TracerBuilder builder,
            Action<HttpClientAdapterOptions> configureHttpAdapterOptions = null,
            Action<SqlClientAdapterOptions> configureSqlAdapterOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var httpOptions = new HttpClientAdapterOptions();
            configureHttpAdapterOptions?.Invoke(httpOptions);

            var sqlOptions = new SqlClientAdapterOptions();
            configureSqlAdapterOptions?.Invoke(sqlOptions);

            return builder
                .AddAdapter((t) => new AzureClientsAdapter(t))
                .AddAdapter((t) => new AzurePipelineAdapter(t))
                .AddAdapter((t) => new HttpClientAdapter(t, httpOptions))
                .AddAdapter((t) => new HttpWebRequestAdapter(t, httpOptions))
                .AddAdapter((t) => new SqlClientAdapter(t, sqlOptions));
        }
    }
}
