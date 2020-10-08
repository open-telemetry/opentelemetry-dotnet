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
using OpenTelemetry;

namespace OpenTelemetry.Exporter
{
    public class InMemoryExporter<T> : BaseExporter<T>
        where T : class
    {
        private readonly IList<T> trait;

        public InMemoryExporter(InMemoryExporterOptions options)
        {
            if (options.Trait == null)
            {
                throw new ArgumentNullException("nameof(options.Trait)");
            }

            this.trait = (IList<T>)options.Trait;
        }

        public override ExportResult Export(in Batch<T> batch)
        {
            foreach (var data in batch)
            {
                this.trait.Add(data);
            }

            return ExportResult.Success;
        }
    }
}
