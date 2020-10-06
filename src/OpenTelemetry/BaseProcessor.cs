// <copyright file="BaseProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    /// <summary>
    /// Base processor base class.
    /// </summary>
    /// <typeparam name="T">The type of object to be processed.</typeparam>
    public abstract class BaseProcessor<T> : IDisposable
    {
        private int shutdownCount;

        /// <summary>
        /// Called synchronously when a telemetry object is started.
        /// </summary>
        /// <param name="data">
        /// The started telemetry object.
        /// </param>
        /// <remarks>
        /// This function is called synchronously on the thread which started
        /// the telemetry object. This function should be thread-safe, and
        /// should not block indefinitely or throw exceptions.
        /// </remarks>
        public virtual void OnStart(T data)
        {
        }

        /// <summary>
        /// Called synchronously when a telemetry object is ended.
        /// </summary>
        /// <param name="data">
        /// The ended telemetry object.
        /// </param>
        /// <remarks>
        /// This function is called synchronously on the thread which ended
        /// the telemetry object. This function should be thread-safe, and
        /// should not block indefinitely or throw exceptions.
        /// </remarks>
        public virtual void OnEnd(T data)
        {
        }

        /// <summary>
        /// Flushes the processor, blocks the current thread until flush
        /// completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when flush succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety.
        /// </remarks>
        public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            }

            try
            {
                return this.OnForceFlush(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ForceFlush), ex);
                return false;
            }
        }

        /// <summary>
        /// Attempts to shutdown the processor, blocks the current thread until
        /// shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety. Only the first call will
        /// win, subsequent calls will be no-op.
        /// </remarks>
        public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            }

            if (Interlocked.Increment(ref this.shutdownCount) > 1)
            {
                return false; // shutdown already called
            }

            try
            {
                return this.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Shutdown), ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called by <c>ForceFlush</c>. This function should block the current
        /// thread until flush completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when flush succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which called
        /// <c>ForceFlush</c>. This function should be thread-safe, and should
        /// not throw exceptions.
        /// </remarks>
        protected virtual bool OnForceFlush(int timeoutMilliseconds)
        {
            return true;
        }

        /// <summary>
        /// Called by <c>Shutdown</c>. This function should block the current
        /// thread until shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which made the
        /// first call to <c>Shutdown</c>. This function should not throw
        /// exceptions.
        /// </remarks>
        protected virtual bool OnShutdown(int timeoutMilliseconds)
        {
            return true;
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally
        /// releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release both managed and unmanaged resources;
        /// <see langword="false"/> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
