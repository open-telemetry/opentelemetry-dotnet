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
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of ASP.NET Core request instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Enables the incoming requests automatic data collection for ASP.NET Core.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="configureAspNetCoreInstrumentationOptions">ASP.NET Core Request configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddAspNetCoreInstrumentation(
            this TracerProviderBuilder builder,
            Action<AspNetCoreInstrumentationOptions> configureAspNetCoreInstrumentationOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var aspnetCoreOptions = new AspNetCoreInstrumentationOptions();
            configureAspNetCoreInstrumentationOptions?.Invoke(aspnetCoreOptions);
            builder.AddInstrumentation(() => new AspNetCoreInstrumentation(aspnetCoreOptions));
            builder.AddSource(HttpInListener.ActivitySourceName);
            builder.AddLegacySource(HttpInListener.ActivityOperationName); // for the activities created by AspNetCore
            builder.AddLegacySource(HttpInListener.ActivityNameByHttpInListener); // for the sibling activities created by the instrumentation library

            return builder;
        }
    }
}
