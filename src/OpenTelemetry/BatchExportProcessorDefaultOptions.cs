// <copyright file="BatchExportProcessorDefaultOptions.cs" company="OpenTelemetry Authors">
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

    internal static class BatchExportProcessorDefaultOptions
    {
        public const int MaxQueueSize = 2048;
        public const int ScheduledDelayMilliseconds = 5000;
        public const int ExporterTimeoutMilliseconds = 30000;
        public const int MaxExportBatchSize = 512;
    }
}
