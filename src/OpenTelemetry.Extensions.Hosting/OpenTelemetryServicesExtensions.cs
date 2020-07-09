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

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Extensions.Hosting.Implementation;
    using OpenTelemetry.Trace.Configuration;

    /// <summary>
    /// Extension methods for setting up OpenTelemetry services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class OpenTelemetryServicesExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services)
        {
            services.AddOpenTelemetry(builder => { });
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="OpenTelemetryBuilder"/> configuration delegate.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Action<OpenTelemetryBuilder> configure)
        {
            services.AddOpenTelemetry(() => OpenTelemetrySdk.EnableOpenTelemetry(configure));
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="OpenTelemetryBuilder"/> configuration delegate.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Action<IServiceProvider, OpenTelemetryBuilder> configure)
        {
            services.AddOpenTelemetry(s => OpenTelemetrySdk.EnableOpenTelemetry(builder => configure(s, builder)));
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createSdk">A delegate that provides the factory to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Func<OpenTelemetrySdk> createSdk)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (createSdk is null)
            {
                throw new ArgumentNullException(nameof(createSdk));
            }

            services.AddSingleton(s => createSdk());
            AddOpenTelemetryInternal(services);

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createSdk">A delegate that provides the factory to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Func<IServiceProvider, OpenTelemetrySdk> createSdk)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (createSdk is null)
            {
                throw new ArgumentNullException(nameof(createSdk));
            }

            services.AddSingleton(s => createSdk(s));
            AddOpenTelemetryInternal(services);

            return services;
        }

        private static void AddOpenTelemetryInternal(IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
        }
    }
}
