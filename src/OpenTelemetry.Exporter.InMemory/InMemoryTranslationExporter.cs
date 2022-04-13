// <copyright file="InMemoryTranslationExporter.cs" company="OpenTelemetry Authors">
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
    public class InMemoryTranslationExporter<Tinput, Toutput> : BaseExporter<Tinput>
        where Tinput : class
        where Toutput : class
    {
        private readonly ICollection<Toutput> exportedItems;
        private readonly Func<Tinput, Toutput> translation;

        public InMemoryTranslationExporter(ICollection<Toutput> exportedItems, Func<Tinput, Toutput> translation)
        {
            this.exportedItems = exportedItems;
            this.translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public override ExportResult Export(in Batch<Tinput> batch)
        {
            if (this.exportedItems == null)
            {
                return ExportResult.Failure;
            }

            foreach (var data in batch)
            {
                this.exportedItems.Add(this.translation.Invoke(data));
            }

            return ExportResult.Success;
        }
    }
}
