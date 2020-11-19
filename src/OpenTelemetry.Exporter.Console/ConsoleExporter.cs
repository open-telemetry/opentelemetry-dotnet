// <copyright file="ConsoleExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter
{
    public class ConsoleExporter<T> : BaseExporter<T>
        where T : class
    {
        private readonly ConsoleExporterOptions options;
        private Action<T, Action<string>> exportMethod;

        public ConsoleExporter(ConsoleExporterOptions options)
        {
            this.options = options ?? new ConsoleExporterOptions();
        }

        public override ExportResult Export(in Batch<T> batch)
        {
            if (this.exportMethod == null)
            {
                return ExportResult.Failure;
            }

            foreach (var item in batch)
            {
                this.exportMethod(item, this.WriteLine);
            }

            return ExportResult.Success;
        }

        internal void Init(Action<T, Action<string>> exportMethod)
        {
            this.exportMethod = exportMethod;
        }

        private void WriteLine(string message)
        {
            if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Console))
            {
                System.Console.WriteLine(message);
            }

            if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug))
            {
                Debug.WriteLine(message);
            }
        }
    }
}
