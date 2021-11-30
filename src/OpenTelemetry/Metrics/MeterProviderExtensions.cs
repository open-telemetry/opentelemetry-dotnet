// <copyright file="MeterProviderExtensions.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public static class MeterProviderExtensions
    {
        /// <summary>
        /// Flushes all the readers registered under MeterProviderSdk, blocks the current thread
        /// until flush completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="provider">MeterProviderSdk instance on which ForceFlush will be called.</param>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when force flush succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety.
        /// </remarks>
        public static bool ForceFlush(this MeterProvider provider, int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.Null(provider, nameof(provider));
            Guard.InvalidTimeout(timeoutMilliseconds, nameof(timeoutMilliseconds));

            if (provider is MeterProviderSdk meterProviderSdk)
            {
                try
                {
                    return meterProviderSdk.OnForceFlush(timeoutMilliseconds);
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.MeterProviderException(nameof(meterProviderSdk.OnForceFlush), ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to shutdown the MeterProviderSdk, blocks the current thread until
        /// shutdown completed or timed out.
        /// </summary>
        /// <param name="provider">MeterProviderSdk instance on which Shutdown will be called.</param>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety. Only the first call will
        /// win, subsequent calls will be no-op.
        /// </remarks>
        public static bool Shutdown(this MeterProvider provider, int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.Null(provider, nameof(provider));
            Guard.InvalidTimeout(timeoutMilliseconds, nameof(timeoutMilliseconds));

            if (provider is MeterProviderSdk meterProviderSdk)
            {
                if (Interlocked.Increment(ref meterProviderSdk.ShutdownCount) > 1)
                {
                    return false; // shutdown already called
                }

                try
                {
                    return meterProviderSdk.OnShutdown(timeoutMilliseconds);
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.MeterProviderException(nameof(meterProviderSdk.OnShutdown), ex);
                    return false;
                }
            }

            return true;
        }

        public static bool TryFindExporter<T>(this MeterProvider provider, out T exporter)
            where T : BaseExporter<Metric>
        {
            if (provider is MeterProviderSdk meterProviderSdk)
            {
                return TryFindExporter(meterProviderSdk.Reader, out exporter);
            }

            exporter = null;
            return false;

            static bool TryFindExporter(MetricReader reader, out T exporter)
            {
                if (reader is BaseExportingMetricReader exportingMetricReader)
                {
                    exporter = exportingMetricReader.Exporter as T;
                    return exporter != null;
                }

                if (reader is CompositeMetricReader compositeMetricReader)
                {
                    foreach (MetricReader childReader in compositeMetricReader)
                    {
                        if (TryFindExporter(childReader, out exporter))
                        {
                            return true;
                        }
                    }
                }

                exporter = null;
                return false;
            }
        }
    }
}
