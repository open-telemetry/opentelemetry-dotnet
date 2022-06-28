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
using OpenTelemetry.Internal;

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
            Guard.ThrowIfNull(builder);

            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    AddAspNetCoreInstrumentation(builder, sp.GetOptions<AspNetCoreInstrumentationOptions>(), configureAspNetCoreInstrumentationOptions);
                });
            }

            return AddAspNetCoreInstrumentation(builder, new AspNetCoreInstrumentationOptions(), configureAspNetCoreInstrumentationOptions);
        }

        /// <summary>
        /// Adds ASP.NET Core sources to instrumentation list.
        /// ASP.NET Core instrumentation must be added externally when ready to instrument.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        /// <remarks>
        /// This method is required for auto-instrumentation.
        /// </remarks>
        public static TracerProviderBuilder AddAspNetCoreSources(this TracerProviderBuilder builder)
        {
            // Important: Do NOT reference external libraries such as ASP.NET framework.
            // It will trigger assembly load and break auto-instrumentation.

            builder.AddSource(InstrumentationInfo.ActivitySourceName);
            builder.AddLegacySource(InstrumentationInfo.ActivityOperationName); // for the activities created by AspNetCore

            return builder;
        }

        internal static TracerProviderBuilder AddAspNetCoreInstrumentation(
            this TracerProviderBuilder builder,
            AspNetCoreInstrumentation instrumentation)
        {
            builder.AddAspNetCoreSources();
            return builder.AddInstrumentation(() => instrumentation);
        }

        private static TracerProviderBuilder AddAspNetCoreInstrumentation(
            TracerProviderBuilder builder,
            AspNetCoreInstrumentationOptions options,
            Action<AspNetCoreInstrumentationOptions> configure = null)
        {
            configure?.Invoke(options);
            return AddAspNetCoreInstrumentation(
                builder,
                new AspNetCoreInstrumentation(new HttpInListener(options)));
        }
    }
}
