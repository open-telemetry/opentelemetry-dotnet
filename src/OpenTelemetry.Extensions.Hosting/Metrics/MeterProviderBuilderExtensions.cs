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
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="MeterProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        [Obsolete("Call ConfigureBuilder instead this method will be removed in a future version.")]
        public static MeterProviderBuilder Configure(this MeterProviderBuilder meterProviderBuilder, Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            return meterProviderBuilder.ConfigureBuilder(configure);
        }

        /// <summary>
        /// Gets the application <see cref="IServiceCollection"/> attached to
        /// the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns><see cref="IServiceCollection"/> or <see langword="null"/>
        /// if services are unavailable.</returns>
        [Obsolete("Call ConfigureServices instead this method will be removed in a future version.")]
        public static IServiceCollection GetServices(this MeterProviderBuilder meterProviderBuilder)
        {
            IServiceCollection services = null;
            meterProviderBuilder.ConfigureServices(s => services = s);
            return services;
        }
    }
}
