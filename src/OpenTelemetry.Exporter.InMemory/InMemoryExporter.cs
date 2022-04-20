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
        private readonly ExportDelegate onExport;

        public InMemoryExporter(ICollection<T> exportedItems)
        {
            if (typeof(T) == typeof(Metrics.Metric))
            {
                // TODO: users should be discouraged from using the InMemoryExporter
                // in this way for Metrics. Exported Metrics are not trustworthy
                // because they can continue to be updated after export.
                throw new NotSupportedException("TODO: MESSAGE");
            }

            this.exportedItems = exportedItems;
            this.onExport = this.DefaultExport;
        }

        public InMemoryExporter(Func<Batch<T>, ExportResult> exportFunc)
        {
            this.onExport = (in Batch<T> batch) => exportFunc(batch);
        }

        private delegate ExportResult ExportDelegate(in Batch<T> batch);

        public override ExportResult Export(in Batch<T> batch) => this.onExport(batch);

        private ExportResult DefaultExport(in Batch<T> batch)
        {
            if (this.exportedItems == null)
            {
                return ExportResult.Failure;
            }

            foreach (var data in batch)
            {
                this.exportedItems?.Add(data);
            }

            return ExportResult.Success;
        }
    }
}
