// <copyright file="MeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of HttpClient instrumentation.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddHttpClientInstrumentation(
            this MeterProviderBuilder builder) =>
            builder.AddHttpClientInstrumentation(null);

        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="configureOptions">Callback action for configuring <see cref="HttpClientInstrumentationMeterOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddHttpClientInstrumentation(
            this MeterProviderBuilder builder,
            Action<HttpClientInstrumentationMeterOptions> configureOptions) =>
            builder.AddHttpClientInstrumentation(optionsName: null, configureOptions);

        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="optionsName"> Name which is used when retrieving options.</param>
        /// <param name="configureOptions">Callback action for configuring <see cref="HttpClientInstrumentationMeterOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddHttpClientInstrumentation(
            this MeterProviderBuilder builder,
            string optionsName,
            Action<HttpClientInstrumentationMeterOptions> configureOptions)
        {
            // TODO: Implement an IDeferredMeterProviderBuilder

            Guard.ThrowIfNull(builder);

            optionsName ??= Options.DefaultName;

            if (configureOptions != null)
            {
                builder.ConfigureServices(services => services.Configure(optionsName, configureOptions));
            }

            builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<HttpClientInstrumentationMeterOptions>>().Get(optionsName);
                var instrumentation = new HttpClientMetrics(options);
                builder.AddMeter(HttpClientMetrics.InstrumentationName);
                builder.AddInstrumentation(() => instrumentation);
            });

            return builder;
        }
    }
}
