// <copyright file="OpenTelemetryServicesExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up OpenTelemetry services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class OpenTelemetryServicesExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry TracerProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services)
        {
            services.AddOpenTelemetryTracing(builder => { });
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry TracerProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="TracerProviderBuilder"/> action to configure TracerProviderBuilder.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Action<TracerProviderBuilder> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = Sdk.CreateTracerProviderBuilder();
            configure(builder);
            services.AddOpenTelemetryTracing(() => builder.Build());
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry TracerProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="TracerProviderBuilder"/> action to configure TracerProviderBuilder.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = Sdk.CreateTracerProviderBuilder();
            services.AddOpenTelemetryTracing((sp) =>
            {
                configure(sp, builder);
                return builder.Build();
            });
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry TracerProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createTracerProvider">A delegate that provides the tracer provider to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Func<TracerProvider> createTracerProvider)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (createTracerProvider is null)
            {
                throw new ArgumentNullException(nameof(createTracerProvider));
            }

            try
            {
                services.AddSingleton(s => createTracerProvider());
                AddOpenTelemetryTracingInternal(services);
            }
            catch (Exception ex)
            {
                HostingExtensionsEventSource.Log.FailedInitialize(ex);
            }

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry TracerProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createTracerProvider">A delegate that provides the tracer provider to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Func<IServiceProvider, TracerProvider> createTracerProvider)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (createTracerProvider is null)
            {
                throw new ArgumentNullException(nameof(createTracerProvider));
            }

            try
            {
                services.AddSingleton(s => createTracerProvider(s));
                AddOpenTelemetryTracingInternal(services);
            }
            catch (Exception ex)
            {
                HostingExtensionsEventSource.Log.FailedInitialize(ex);
            }

            return services;
        }

        private static void AddOpenTelemetryTracingInternal(IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
        }
    }
}
