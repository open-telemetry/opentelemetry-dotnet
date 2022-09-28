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

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
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
            Guard.ThrowIfNull(textMapPropagator);

            Propagators.DefaultTextMapPropagator = textMapPropagator;
        }

        /// <summary>
        /// Creates a <see cref="MeterProviderBuilder"/> which is used to build
        /// a <see cref="MeterProvider"/>. In a typical application, a single
        /// <see cref="MeterProvider"/> is created at application startup and disposed
        /// at application shutdown. It is important to ensure that the provider is not
        /// disposed too early.
        /// </summary>
        /// <returns><see cref="MeterProviderBuilder"/> instance, which is used to build a <see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder CreateMeterProviderBuilder()
        {
            return new MeterProviderBuilderSdk();
        }

        /// <summary>
        /// Creates a <see cref="TracerProviderBuilder"/> which is used to build
        /// a <see cref="TracerProvider"/>. In a typical application, a single
        /// <see cref="TracerProvider"/> is created at application startup and disposed
        /// at application shutdown. It is important to ensure that the provider is not
        /// disposed too early.
        /// </summary>
        /// <returns><see cref="TracerProviderBuilder"/> instance, which is used to build a <see cref="TracerProvider"/>.</returns>
        public static TracerProviderBuilder CreateTracerProviderBuilder()
        {
            return new TracerProviderBuilderSdk();
        }
    }
}
