﻿// <copyright file="ActivityExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Trace.Export
{
    /// <summary>
    /// Enumeration used to define the result of an export operation.
    /// </summary>
    public enum ExportResult
    {
        /// <summary>
        /// Batch is successfully exported.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Batch export failed. Caller must not retry.
        /// </summary>
        FailedNotRetryable = 1,

        /// <summary>
        /// Batch export failed transiently. Caller should record error and may retry.
        /// </summary>
        FailedRetryable = 2,
    }

    /// <summary>
    /// ActivityExporter base class.
    /// </summary>
    public abstract class ActivityExporter : IDisposable
    {
        /// <summary>
        /// Exports batch of activities asynchronously.
        /// </summary>
        /// <param name="batch">Batch of activities to export.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of export.</returns>
        public abstract Task<ExportResult> ExportAsync(IEnumerable<Activity> batch, CancellationToken cancellationToken);

        /// <summary>
        /// Shuts down exporter asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns <see cref="Task"/>.</returns>
        public abstract Task ShutdownAsync(CancellationToken cancellationToken);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
