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
using OpenTelemetry.Instrumentation.Http;
#if NETFRAMEWORK
using OpenTelemetry.Instrumentation.Http.Implementation;
#endif

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
#if NETFRAMEWORK
        /// <summary>
        /// Enables HttpClient and HttpWebRequest instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="configureHttpClientInstrumentationOptions">HttpClient configuration options.</param>
        /// <param name="configureHttpWebRequestInstrumentationOptions">HttpWebRequest configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddHttpClientInstrumentation(
            this TracerProviderBuilder builder,
            Action<HttpClientInstrumentationOptions> configureHttpClientInstrumentationOptions = null,
            Action<HttpWebRequestInstrumentationOptions> configureHttpWebRequestInstrumentationOptions = null)
#else
        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="configureHttpClientInstrumentationOptions">HttpClient configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddHttpClientInstrumentation(
            this TracerProviderBuilder builder,
            Action<HttpClientInstrumentationOptions> configureHttpClientInstrumentationOptions = null)
#endif
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var httpClientOptions = new HttpClientInstrumentationOptions();

            configureHttpClientInstrumentationOptions?.Invoke(httpClientOptions);

            builder.AddInstrumentation((activitySource) => new HttpClientInstrumentation(activitySource, httpClientOptions));

#if NETFRAMEWORK
            builder.AddHttpWebRequestInstrumentation(configureHttpWebRequestInstrumentationOptions);
#endif
            return builder;
        }

#if NETFRAMEWORK
        internal static TracerProviderBuilder AddHttpWebRequestInstrumentation(
            this TracerProviderBuilder builder,
            Action<HttpWebRequestInstrumentationOptions> configureOptions = null)
        {
            HttpWebRequestInstrumentationOptions options = new HttpWebRequestInstrumentationOptions();

            configureOptions?.Invoke(options);

            HttpWebRequestActivitySource.Options = options;

            builder.AddSource(HttpWebRequestActivitySource.ActivitySourceName);

            return builder;
        }
#endif
    }
}
