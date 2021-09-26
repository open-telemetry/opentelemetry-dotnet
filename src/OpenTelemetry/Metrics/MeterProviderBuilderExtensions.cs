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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds a reader to the provider.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="reader"><see cref="MetricReader"/>.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddReader(this MeterProviderBuilder meterProviderBuilder, MetricReader reader)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.AddReader(reader);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// </summary>
        /// <param name="meterProviderBuilder">MeterProviderBuilder instance.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder SetResourceBuilder(this MeterProviderBuilder meterProviderBuilder, ResourceBuilder resourceBuilder)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.SetResourceBuilder(resourceBuilder);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Run the given actions to initialize the <see cref="MeterProvider"/>.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProvider Build(this MeterProviderBuilder meterProviderBuilder)
        {
            if (meterProviderBuilder is IDeferredMeterProviderBuilder)
            {
                throw new NotSupportedException("DeferredMeterProviderBuilder requires a ServiceProvider to build.");
            }

            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.BuildSdk();
            }

            return null;
        }
    }
}
