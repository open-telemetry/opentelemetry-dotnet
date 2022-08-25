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
#if NET7_0_OR_GREATER
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
#endif
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
                    AddAspNetCoreInstrumentation(builder, sp.GetOptions<AspNetCoreInstrumentationOptions>(), configureAspNetCoreInstrumentationOptions, sp);
                });
            }

            return AddAspNetCoreInstrumentation(builder, new AspNetCoreInstrumentationOptions(), configureAspNetCoreInstrumentationOptions);
        }

        internal static TracerProviderBuilder AddAspNetCoreInstrumentation(
            this TracerProviderBuilder builder,
            AspNetCoreInstrumentation instrumentation,
            IServiceProvider serviceProvider = null)
        {
            // For .NET7.0 onwards activity will be created using activitySource.
            // https://github.com/dotnet/aspnetcore/blob/bf3352f2422bf16fa3ca49021f0e31961ce525eb/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L327
            // For .NET6.0 and below, we will continue to use legacy way.
#if NET7_0_OR_GREATER
            // TODO: Check with .NET team to see if this can be prevented
            // as this allows user to override the ActivitySource.
            var activitySourceService = serviceProvider?.GetService<ActivitySource>();
            if (activitySourceService != null)
            {
                builder.AddSource(activitySourceService.Name);
            }
            else
            {
                // For users not using hosting package?
                builder.AddSource(HttpInListener.AspNetCoreActivitySourceName);
            }
#else
            builder.AddSource(HttpInListener.ActivitySourceName);
            builder.AddLegacySource(HttpInListener.ActivityOperationName); // for the activities created by AspNetCore
#endif

            return builder.AddInstrumentation(() => instrumentation);
        }

        private static TracerProviderBuilder AddAspNetCoreInstrumentation(
            TracerProviderBuilder builder,
            AspNetCoreInstrumentationOptions options,
            Action<AspNetCoreInstrumentationOptions> configure = null,
            IServiceProvider serviceProvider = null)
        {
            configure?.Invoke(options);
            return AddAspNetCoreInstrumentation(
                builder,
                new AspNetCoreInstrumentation(new HttpInListener(options)),
                serviceProvider);
        }
    }
}
