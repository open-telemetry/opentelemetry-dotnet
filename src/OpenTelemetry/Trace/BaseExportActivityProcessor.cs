// <copyright file="BaseExportActivityProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Implements processor that exports <see cref="Activity"/> objects.
    /// </summary>
    public abstract class BaseExportActivityProcessor : ActivityProcessor
    {
        protected readonly ActivityExporter exporter;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseExportActivityProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Activity exporter instance.</param>
        public BaseExportActivityProcessor(ActivityExporter exporter)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <inheritdoc />
        public sealed override void OnStart(Activity activity)
        {
        }

        /// <inheritdoc />
        public abstract override void OnEnd(Activity activity);

        /// <inheritdoc />
        protected override void OnShutdown(int timeoutMilliseconds)
        {
            this.exporter.Shutdown(timeoutMilliseconds);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !this.disposed)
            {
                try
                {
                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }

                this.disposed = true;
            }
        }
    }
}
