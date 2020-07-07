// <copyright file="OpenTelemetryBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Dependencies.Implementation;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class OpenTelemetryBuilderExtensions
    {
        /// <summary>
        /// Enables the outgoing requests automatic data collection for all supported activity sources.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddDependencyInstrumentation(this OpenTelemetryBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddHttpClientDependencyInstrumentation();
            builder.AddSqlClientDependencyInstrumentation();
            builder.AddGrpcClientDependencyInstrumentation();
#if NETFRAMEWORK
            builder.AddHttpWebRequestDependencyInstrumentation();
#endif
            return builder;
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection for all supported activity sources.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <param name="configureHttpClientInstrumentationOptions">HttpClient configuration options.</param>
        /// <param name="configureSqlClientInstrumentationOptions">SqlClient configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddDependencyInstrumentation(
            this OpenTelemetryBuilder builder,
            Action<HttpClientInstrumentationOptions> configureHttpClientInstrumentationOptions = null,
            Action<SqlClientInstrumentationOptions> configureSqlClientInstrumentationOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddHttpClientDependencyInstrumentation(configureHttpClientInstrumentationOptions);
            builder.AddSqlClientDependencyInstrumentation(configureSqlClientInstrumentationOptions);
            builder.AddGrpcClientDependencyInstrumentation();
#if NETFRAMEWORK
            builder.AddHttpWebRequestDependencyInstrumentation();
#endif
            return builder;
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection for HttpClient.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddHttpClientDependencyInstrumentation(
            this OpenTelemetryBuilder builder)
        {
            return builder.AddHttpClientDependencyInstrumentation(null);
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection for HttpClient.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <param name="configureHttpClientInstrumentationOptions">HttpClient configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddHttpClientDependencyInstrumentation(
            this OpenTelemetryBuilder builder,
            Action<HttpClientInstrumentationOptions> configureHttpClientInstrumentationOptions)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var httpClientOptions = new HttpClientInstrumentationOptions();
            configureHttpClientInstrumentationOptions?.Invoke(httpClientOptions);

            builder.AddInstrumentation((activitySource) => new HttpClientInstrumentation(activitySource, httpClientOptions));
            return builder;
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection for SqlClient.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddSqlClientDependencyInstrumentation(
            this OpenTelemetryBuilder builder)
        {
            return builder.AddSqlClientDependencyInstrumentation(null);
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection for SqlClient.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <param name="configureSqlClientInstrumentationOptions">SqlClient configuration options.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddSqlClientDependencyInstrumentation(
            this OpenTelemetryBuilder builder,
            Action<SqlClientInstrumentationOptions> configureSqlClientInstrumentationOptions)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var sqlOptions = new SqlClientInstrumentationOptions();
            configureSqlClientInstrumentationOptions?.Invoke(sqlOptions);

            builder.AddInstrumentation((activitySource) => new SqlClientInstrumentation(activitySource, sqlOptions));
#if NETFRAMEWORK
            builder.AddActivitySource(SqlEventSourceListener.ActivitySourceName);
#endif

            return builder;
        }

        /// <summary>
        /// Enables the outgoing requests automatic data collection for GrpcClient.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddGrpcClientDependencyInstrumentation(
            this OpenTelemetryBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddInstrumentation((activitySource) => new GrpcClientInstrumentation(activitySource));
            return builder;
        }

#if NETFRAMEWORK
        /// <summary>
        /// Enables the outgoing requests automatic data collection for .NET Framework HttpWebRequest activity source.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddHttpWebRequestDependencyInstrumentation(this OpenTelemetryBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            GC.KeepAlive(HttpWebRequestActivitySource.Instance);

            builder.AddActivitySource(HttpWebRequestActivitySource.ActivitySourceName);

            return builder;
        }
#endif
    }
}
