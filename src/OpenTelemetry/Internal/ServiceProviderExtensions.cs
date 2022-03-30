// <copyright file="ServiceProviderExtensions.cs" company="OpenTelemetry Authors">
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

#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using Microsoft.Extensions.Options;
#endif

namespace System
{
    /// <summary>
    /// Extension methods for OpenTelemetry dependency injection support.
    /// </summary>
    internal static class ServiceProviderExtensions
    {
        /// <summary>
        /// Get options from the supplied <see cref="IServiceProvider"/>.
        /// </summary>
        /// <typeparam name="T">Options type.</typeparam>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <returns>Options instance.</returns>
        public static T GetOptions<T>(this IServiceProvider serviceProvider)
            where T : class, new()
        {
#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            IOptions<T> options = (IOptions<T>)serviceProvider.GetService(typeof(IOptions<T>));

            // Note: options could be null if user never invoked services.AddOptions().
            return options?.Value ?? new T();
#else
            return new T();
#endif
        }
    }
}
