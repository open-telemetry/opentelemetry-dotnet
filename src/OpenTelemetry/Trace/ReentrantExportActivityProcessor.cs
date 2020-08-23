﻿// <copyright file="ReentrantExportActivityProcessor.cs" company="OpenTelemetry Authors">
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
    /// Implements activity processor that exports <see cref="Activity"/> at each OnEnd call without synchronization.
    /// </summary>
    public class ReentrantExportActivityProcessor : ActivityProcessor
    {
        private readonly ActivityExporter exporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReentrantExportActivityProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Activity exporter instance.</param>
        public ReentrantExportActivityProcessor(ActivityExporter exporter)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <inheritdoc />
        public override void OnEnd(Activity activity)
        {
            try
            {
                _ = this.exporter.Export(new Batch<Activity>(activity));
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.OnEnd), ex);
            }
        }

        protected override void OnShutdown(int timeoutMilliseconds)
        {
            this.exporter.Shutdown(timeoutMilliseconds);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                try
                {
                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }
            }
        }
    }
}
