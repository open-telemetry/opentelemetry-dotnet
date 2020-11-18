// <copyright file="InMemoryExporterLoggingExtensions.cs" company="OpenTelemetry Authors">
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

#if NET461 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
using OpenTelemetry.Exporter;

namespace OpenTelemetry.Logs
{
    public static class InMemoryExporterLoggingExtensions
    {
        public static OpenTelemetryLoggerOptions AddInMemoryExporter(this OpenTelemetryLoggerOptions loggerOptions, ICollection<LogRecord> exportedItems)
        {
            if (loggerOptions == null)
            {
                throw new ArgumentNullException(nameof(loggerOptions));
            }

            if (exportedItems == null)
            {
                throw new ArgumentNullException(nameof(exportedItems));
            }

            return loggerOptions.AddProcessor(new SimpleExportProcessor<LogRecord>(new InMemoryExporter<LogRecord>(exportedItems)));
        }
    }
}
#endif
