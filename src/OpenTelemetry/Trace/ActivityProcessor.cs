// <copyright file="ActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Activity processor base class.
    /// </summary>
    public abstract class ActivityProcessor : IDisposable
    {
        private bool disposed;

        /// <summary>
        /// Activity start hook.
        /// </summary>
        /// <param name="activity">Instance of activity to process.</param>
        public virtual void OnStart(Activity activity)
        {
        }

        /// <summary>
        /// Activity end hook.
        /// </summary>
        /// <param name="activity">Instance of activity to process.</param>
        public virtual void OnEnd(Activity activity)
        {
        }

        /// <summary>
        /// Flushes the <see cref="ActivityProcessor"/>, blocks the current
        /// thread until flush completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when flush completed; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        {
            return true;
        }

        /// <summary>
        /// Attempts to shutdown the processor, blocks the current thread until
        /// shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        public virtual void Shutdown(int timeoutMilliseconds = Timeout.Infinite)
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    this.Shutdown();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }
            }

            this.disposed = true;
        }
    }
}
