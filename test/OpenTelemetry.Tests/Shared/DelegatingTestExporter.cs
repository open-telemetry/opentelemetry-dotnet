// <copyright file="DelegatingTestExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests
{
    public class DelegatingTestExporter<T> : BaseExporter<T>
        where T : class
    {
        private readonly BaseExporter<T> exporter;
        private readonly Action onExportAction;

        public DelegatingTestExporter(
            BaseExporter<T> exporter,
            Action onExportAction = null)
        {
            this.exporter = exporter;
            this.onExportAction = onExportAction;
        }

        public List<ExportResult> ExportResults { get; } = new();

        public override ExportResult Export(in Batch<T> batch)
        {
            var result = this.exporter.Export(batch);
            this.ExportResults.Add(result);
            this.onExportAction?.Invoke();
            return result;
        }
    }
}
