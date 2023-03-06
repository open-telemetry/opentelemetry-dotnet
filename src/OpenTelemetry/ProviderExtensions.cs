// <copyright file="ProviderExtensions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry
{
    /// <summary>
    /// Contains provider extension methods.
    /// </summary>
    public static class ProviderExtensions
    {
        /// <summary>
        /// Gets the <see cref="Resource"/> associated with the <see cref="BaseProvider"/>.
        /// </summary>
        /// <param name="baseProvider"><see cref="BaseProvider"/>.</param>
        /// <returns><see cref="Resource"/>if found otherwise <see cref="Resource.Empty"/>.</returns>
        public static Resource GetResource(this BaseProvider baseProvider)
        {
            if (baseProvider is TracerProviderSdk tracerProviderSdk)
            {
                return tracerProviderSdk.Resource;
            }
            else if (baseProvider is OpenTelemetryLoggerProvider otelLoggerProvider)
            {
                return otelLoggerProvider.Resource;
            }
            else if (baseProvider is MeterProviderSdk meterProviderSdk)
            {
                return meterProviderSdk.Resource;
            }

            return Resource.Empty;
        }

        /// <summary>
        /// Gets the <see cref="Resource"/> associated with the <see cref="BaseProvider"/>.
        /// </summary>
        /// <param name="baseProvider"><see cref="BaseProvider"/>.</param>
        /// <returns><see cref="Resource"/>if found otherwise <see cref="Resource.Empty"/>.</returns>
        public static Resource GetDefaultResource(this BaseProvider baseProvider)
        {
            var builder = ResourceBuilder.CreateDefault();
            builder.ServiceProvider = GetServiceProvider(baseProvider);
            return builder.Build();
        }

        internal static IServiceProvider? GetServiceProvider(this BaseProvider baseProvider)
        {
            if (baseProvider is TracerProviderSdk tracerProviderSdk)
            {
                return tracerProviderSdk.ServiceProvider;
            }
            else if (baseProvider is MeterProviderSdk meterProviderSdk)
            {
                return meterProviderSdk.ServiceProvider;
            }
            else if (baseProvider is OpenTelemetryLoggerProvider openTelemetryLoggerProvider)
            {
                return openTelemetryLoggerProvider.ServiceProvider;
            }

            return null;
        }

        internal static Action? GetObservableInstrumentCollectCallback(this BaseProvider baseProvider)
        {
            if (baseProvider is MeterProviderSdk meterProviderSdk)
            {
                return meterProviderSdk.CollectObservableInstruments;
            }

            return null;
        }
    }
}
