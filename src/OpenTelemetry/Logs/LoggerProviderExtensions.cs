// <copyright file="LoggerProviderExtensions.cs" company="OpenTelemetry Authors">
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

using System;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Contains extension methods for the <see cref="LoggerProvider"/> class.
    /// </summary>
    public static class LoggerProviderExtensions
    {
        /// <summary>
        /// Add a processor to the <see cref="LoggerProvider"/>.
        /// </summary>
        /// <remarks>
        /// Note: The supplied <paramref name="processor"/> will be
        /// automatically disposed when then the <see
        /// cref="LoggerProvider"/> is disposed.
        /// </remarks>
        /// <param name="provider"><see cref="LoggerProvider"/> instance on which ForceFlush will be called.</param>
        /// <param name="processor">Log processor to add.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public static LoggerProvider AddProcessor(this LoggerProvider provider, BaseProcessor<LogRecord> processor)
        {
            Guard.ThrowIfNull(provider);
            Guard.ThrowIfNull(processor);

            if (provider is LoggerProviderSdk loggerProviderSdk)
            {
                loggerProviderSdk.AddProcessor(processor);
            }

            return provider;
        }

        /// <summary>
        /// Flushes all the processors registered under <see cref="LoggerProvider"/>, blocks the current thread
        /// until flush completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="provider"><see cref="LoggerProvider"/> instance on which ForceFlush will be called.</param>
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
        public static bool ForceFlush(this LoggerProvider provider, int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.ThrowIfNull(provider);
            Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

            if (provider is LoggerProviderSdk loggerProviderSdk)
            {
                return loggerProviderSdk.ForceFlush(timeoutMilliseconds);
            }

            return true;
        }

        /// <summary>
        /// Attempts to shutdown the <see cref="LoggerProvider"/>, blocks the current thread until
        /// shutdown completed or timed out.
        /// </summary>
        /// <param name="provider"><see cref="LoggerProvider"/> instance on which Shutdown will be called.</param>
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
        public static bool Shutdown(this LoggerProvider provider, int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.ThrowIfNull(provider);
            Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

            if (provider is LoggerProviderSdk loggerProviderSdk)
            {
                return loggerProviderSdk.Shutdown(timeoutMilliseconds);
            }

            return true;
        }
    }
}
