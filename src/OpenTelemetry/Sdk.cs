// <copyright file="Sdk.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry helper.
    /// </summary>
    public static class Sdk
    {
        /// <summary>
        /// Gets a value indicating whether instrumentation is suppressed (disabled).
        /// </summary>
        public static bool SuppressInstrumentation => SuppressInstrumentationScope.IsSuppressed;

        /// <summary>
        /// Creates MeterProviderBuilder which should be used to build MeterProvider.
        /// </summary>
        /// <returns>MeterProviderBuilder instance, which should be used to build MeterProvider.</returns>
        public static MeterProviderBuilder CreateMeterProviderBuilder()
        {
            return new MeterProviderBuilder();
        }

        /// <summary>
        /// Creates TracerProviderBuilder which should be used to build
        /// TracerProvider.
        /// </summary>
        /// <returns>TracerProviderBuilder instance, which should be used to build TracerProvider.</returns>
        public static TracerProviderBuilder CreateTracerProviderBuilder()
        {
            return new TracerProviderBuilder();
        }
    }
}
