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
using OpenTelemetry.Instrumentation.Http.Implementation;
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
        public static MeterProviderBuilder AddHttpClientInstrumentation(this MeterProviderBuilder builder)
        {
            return AddHttpClientInstrumentation(builder, name: null, configureHttpClientMetricsInstrumentationOptions: null);
        }

        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="configureHttpClientMetricsInstrumentationOptions">Callback action for configuring <see cref="HttpClientMetricsInstrumentationOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddHttpClientInstrumentation(this MeterProviderBuilder builder, Action<HttpClientMetricsInstrumentationOptions> configureHttpClientMetricsInstrumentationOptions)
        {
            return AddHttpClientInstrumentation(builder, name: null, configureHttpClientMetricsInstrumentationOptions);
        }

        /// <summary>
        /// Enables HttpClient instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configureHttpClientMetricsInstrumentationOptions">Callback action for configuring <see cref="HttpClientMetricsInstrumentationOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddHttpClientInstrumentation(this MeterProviderBuilder builder, string name, Action<HttpClientMetricsInstrumentationOptions> configureHttpClientMetricsInstrumentationOptions)
        {
            Guard.ThrowIfNull(builder);

            // Note: Warm-up the status code mapping.
            _ = TelemetryHelper.BoxedStatusCodes;

            name ??= Options.DefaultName;

            if (configureHttpClientMetricsInstrumentationOptions != null)
            {
                builder.ConfigureServices(services => services.Configure(name, configureHttpClientMetricsInstrumentationOptions));
            }

            // TODO: Implement an IDeferredMeterProviderBuilder

            // TODO: Handle HttpClientInstrumentationOptions
            //   SetHttpFlavor - seems like this would be handled by views
            //   RecordException - probably doesn't make sense for metric instrumentation

            builder.AddMeter(HttpClientMetrics.InstrumentationName);
            builder.AddInstrumentation(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<HttpClientMetricsInstrumentationOptions>>().Get(name);

                return new HttpClientMetrics(options);
            });

            return builder;
        }
    }
}
