// <copyright file="BatchExportProcessorOptions.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry
{
    public class BatchExportProcessorOptions
    {
        public int MaxQueueSize { get; set; } = BatchExportProcessorDefaultOptions.MaxQueueSize;

        public int ScheduledDelayMilliseconds { get; set; } = BatchExportProcessorDefaultOptions.ScheduledDelayMilliseconds;

        public int ExporterTimeoutMilliseconds { get; set; } = BatchExportProcessorDefaultOptions.ExporterTimeoutMilliseconds;

        public int MaxExportBatchSize { get; set; } = BatchExportProcessorDefaultOptions.MaxExportBatchSize;
    }
}
