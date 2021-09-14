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

using System.Collections.Generic;

namespace OpenTelemetry.Exporter
{
    public class InMemoryExporter<T> : BaseExporter<T>
        where T : class
    {
        private readonly ICollection<T> exportedItems;

        public InMemoryExporter(ICollection<T> exportedItems)
        {
            this.exportedItems = exportedItems;
        }

        public override ExportResult Export(in Batch<T> batch)
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
