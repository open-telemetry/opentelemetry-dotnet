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

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Contains extension methods for the <see cref="TracerProviderBuilder"/> class.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="TracerProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        [Obsolete("Configure has been replaced by factory extensions. This method will be removed in a future version.")]
        public static TracerProviderBuilder Configure(this TracerProviderBuilder tracerProviderBuilder, Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            return (tracerProviderBuilder as IDeferredTracerProviderBuilder)?.Configure(configure);
        }

        /// <summary>
        /// Gets the application <see cref="IServiceCollection"/> attached to
        /// the <see cref="TracerProviderBuilder"/>.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns><see cref="IServiceCollection"/> or <see langword="null"/>
        /// if services are unavailable.</returns>
        [Obsolete("Call ConfigureServices instead this method will be removed in a future version.")]
        public static IServiceCollection GetServices(this TracerProviderBuilder tracerProviderBuilder)
        {
            IServiceCollection services = null;
            tracerProviderBuilder.ConfigureServices(s => services = s);
            return services;
        }
    }
}
