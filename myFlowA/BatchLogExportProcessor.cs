// <copyright file="BatchActivityExportProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Logs;

namespace OpenTelemetry
{
    internal class BatchLogFilteringProcessor : BatchExportProcessor<LogRecord>
    {
        private readonly Func<string, string> filter;
        private readonly BaseExporter<LogRecord> processor;

        public BatchLogFilteringProcessor(
            BaseExporter<LogRecord> processor, Func<string, string> filter) :
            base(processor)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            this.filter = filter;
            this.processor = processor;
        }

        /// <inheritdoc />
        public override void OnEnd(LogRecord data)
        {
            if (data.Equals(null))
            {
                return;
            }

            string a = "a";
            this.filter(a);
            Console.WriteLine(a);

            this.OnExport(data);
        }
    }
}
