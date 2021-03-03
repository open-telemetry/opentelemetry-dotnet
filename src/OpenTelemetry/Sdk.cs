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

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry helper.
    /// </summary>
    public static class Sdk
    {
        static Sdk()
        {
            Propagators.DefaultTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator(),
            });

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
            SelfDiagnostics.EnsureInitialized();
        }

        /// <summary>
        /// Gets a value indicating whether instrumentation is suppressed (disabled).
        /// </summary>
        public static bool SuppressInstrumentation => SuppressInstrumentationScope.IsSuppressed;

        /// <summary>
        /// Sets the Default TextMapPropagator.
        /// </summary>
        /// <param name="textMapPropagator">TextMapPropagator to be set as default.</param>
        public static void SetDefaultTextMapPropagator(TextMapPropagator textMapPropagator)
        {
            Propagators.DefaultTextMapPropagator = textMapPropagator;
        }

        /// <summary>
        /// Creates TracerProviderBuilder which should be used to build
        /// TracerProvider.
        /// </summary>
        /// <returns>TracerProviderBuilder instance, which should be used to build TracerProvider.</returns>
        public static TracerProviderBuilder CreateTracerProviderBuilder()
        {
            return new TracerProviderBuilderSdk();
        }
    }
}
