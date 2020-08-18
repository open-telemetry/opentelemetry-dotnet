// <copyright file="ActivityExporterSync.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Enumeration used to define the result of an export operation.
    /// </summary>
    public enum ExportResultSync
    {
        /// <summary>
        /// Batch export succeeded.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Batch export failed.
        /// </summary>
        Failure = 1,
    }

    /// <summary>
    /// ActivityExporterSync base class.
    /// </summary>
    public abstract class ActivityExporterSync : IDisposable
    {
        /// <summary>
        /// Export a batch of <see cref="Activity"/> objects.
        /// </summary>
        /// <param name="batch">Batch of <see cref="Activity"/> objects to export.</param>
        /// <returns>Result of export.</returns>
        public abstract ExportResultSync Export(in Batch<Activity> batch);

        /// <summary>
        /// Shuts down the exporter.
        /// </summary>
        public virtual void Shutdown()
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
