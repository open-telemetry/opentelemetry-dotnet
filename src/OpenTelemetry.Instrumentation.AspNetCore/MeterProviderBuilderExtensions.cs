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

using System;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Trace
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
        /// <param name="configureAspNetCoreInstrumentationOptions">ASP.NET Core Request configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddAspNetCoreInstrumentation(
            this MeterProviderBuilder builder,
            Action<AspNetCoreInstrumentationOptions> configureAspNetCoreInstrumentationOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // TODO: Currently these options are unused for metrics.
            // Do we need separate options for metrics, or will the same options used for traces make sense for metrics?
            var aspnetCoreOptions = new AspNetCoreInstrumentationOptions();
            configureAspNetCoreInstrumentationOptions?.Invoke(aspnetCoreOptions);
            builder.AddInstrumentation((meter) => new AspNetCoreMetrics(meter, aspnetCoreOptions));

            return builder;
        }
    }
}
