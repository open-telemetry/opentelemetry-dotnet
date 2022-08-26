// <copyright file="InMemoryExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter
{
    public class InMemoryExporter<T> : BaseExporter<T>
        where T : class
    {
        private readonly ICollection<T> exportedItems;
        private readonly ExportFunc onExport;
        private bool disposed;
        private string disposedStackTrace;

        public InMemoryExporter(ICollection<T> exportedItems)
        {
            this.exportedItems = exportedItems;
            this.onExport = this.DefaultExport;
        }

        internal InMemoryExporter(ExportFunc exportFunc)
        {
            this.onExport = exportFunc;
        }

        internal delegate ExportResult ExportFunc(in Batch<T> batch);

        public override ExportResult Export(in Batch<T> batch)
        {
            if (this.disposed)
            {
                // Note: In-memory exporter is designed for testing purposes so this error is strategic to surface lifecycle management bugs during development.
                throw new ObjectDisposedException(
                    this.GetType().Name,
                    $"The in-memory exporter is still being invoked after it has been disposed. This could be the result of invalid lifecycle management of the OpenTelemetry .NET SDK. Dispose was called on the following stack trace:{Environment.NewLine}{this.disposedStackTrace}");
            }

            return this.onExport(batch);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposedStackTrace = Environment.StackTrace;
                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private ExportResult DefaultExport(in Batch<T> batch)
        {
            if (this.exportedItems == null)
            {
                return ExportResult.Failure;
            }

            foreach (var data in batch)
            {
                this.exportedItems.Add(data);
            }

            return ExportResult.Success;
        }
    }
}
