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
            return services.AddOpenTelemetryTracing(builder => { });
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

            var builder = new TracerProviderBuilderHosting(services);
            configure(builder);
            return services.AddOpenTelemetryTracing(sp => builder.Build(sp));
        }

        /// <summary>
        /// Adds OpenTelemetry TracerProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createTracerProvider">A delegate that provides the tracer provider to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        private static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Func<IServiceProvider, TracerProvider> createTracerProvider)
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
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
                return services.AddSingleton(s => createTracerProvider(s));
            }
            catch (Exception ex)
            {
                HostingExtensionsEventSource.Log.FailedInitialize(ex);
            }

            return services;
        }
    }
}
