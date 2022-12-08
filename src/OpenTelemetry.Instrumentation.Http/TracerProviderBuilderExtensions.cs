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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of HttpClient instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddHttpClientInstrumentation(this TracerProviderBuilder builder)
            => AddHttpClientInstrumentation(builder, name: null, configureHttpClientInstrumentationOptions: null);

        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="configureHttpClientInstrumentationOptions">Callback action for configuring <see cref="HttpClientInstrumentationOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddHttpClientInstrumentation(
            this TracerProviderBuilder builder,
            Action<HttpClientInstrumentationOptions> configureHttpClientInstrumentationOptions)
            => AddHttpClientInstrumentation(builder, name: null, configureHttpClientInstrumentationOptions);

        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configureHttpClientInstrumentationOptions">Callback action for configuring <see cref="HttpClientInstrumentationOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddHttpClientInstrumentation(
            this TracerProviderBuilder builder,
            string name,
            Action<HttpClientInstrumentationOptions> configureHttpClientInstrumentationOptions)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            if (configureHttpClientInstrumentationOptions != null)
            {
                builder.ConfigureServices(services => services.Configure(name, configureHttpClientInstrumentationOptions));
            }

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<HttpClientInstrumentationOptions>>().Get(name);

#if NETFRAMEWORK
                HttpWebRequestActivitySource.Options = options;

                builder.AddSource(HttpWebRequestActivitySource.ActivitySourceName);
#else
                AddHttpClientInstrumentation(builder, new HttpClientInstrumentation(options));
#endif
            });
        }

#if !NETFRAMEWORK
        internal static TracerProviderBuilder AddHttpClientInstrumentation(
            this TracerProviderBuilder builder,
            HttpClientInstrumentation instrumentation)
        {
            if (HttpHandlerDiagnosticListener.IsNet7OrGreater)
            {
                builder.AddSource(HttpHandlerDiagnosticListener.HttpClientActivitySourceName);
            }
            else
            {
                builder.AddSource(HttpHandlerDiagnosticListener.ActivitySourceName);
                builder.AddLegacySource("System.Net.Http.HttpRequestOut");
            }

            return builder.AddInstrumentation(() => instrumentation);
        }
#endif
    }
}
