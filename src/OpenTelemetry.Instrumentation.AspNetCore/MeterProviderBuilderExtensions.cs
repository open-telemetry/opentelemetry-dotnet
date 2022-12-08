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
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of ASP.NET Core request instrumentation.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Enables the incoming requests automatic data collection for ASP.NET Core.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddAspNetCoreInstrumentation(
            this MeterProviderBuilder builder)
            => AddAspNetCoreInstrumentation(builder, name: null, configureAspNetCoreInstrumentationOptions: null);

        /// <summary>
        /// Enables the incoming requests automatic data collection for ASP.NET Core.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="configureAspNetCoreInstrumentationOptions">Callback action for configuring <see cref="AspNetCoreMetricsInstrumentationOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddAspNetCoreInstrumentation(
            this MeterProviderBuilder builder,
            Action<AspNetCoreMetricsInstrumentationOptions> configureAspNetCoreInstrumentationOptions)
            => AddAspNetCoreInstrumentation(builder, name: null, configureAspNetCoreInstrumentationOptions);

        /// <summary>
        /// Enables the incoming requests automatic data collection for ASP.NET Core.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configureAspNetCoreInstrumentationOptions">Callback action for configuring <see cref="AspNetCoreMetricsInstrumentationOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddAspNetCoreInstrumentation(
            this MeterProviderBuilder builder,
            string name,
            Action<AspNetCoreMetricsInstrumentationOptions> configureAspNetCoreInstrumentationOptions)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            if (configureAspNetCoreInstrumentationOptions != null)
            {
                builder.ConfigureServices(services => services.Configure(name, configureAspNetCoreInstrumentationOptions));
            }

            builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<AspNetCoreMetricsInstrumentationOptions>>().Get(name);

                // TODO: Add additional options to AspNetCoreMetricsInstrumentationOptions ?
                //   RecordException - probably doesn't make sense for metric instrumentation
                //   EnableGrpcAspNetCoreSupport - this instrumentation will also need to also handle gRPC requests

                var instrumentation = new AspNetCoreMetrics(options);
                builder.AddMeter(AspNetCoreMetrics.InstrumentationName);
                builder.AddInstrumentation(() => instrumentation);
            });

            return builder;
        }
    }
}
