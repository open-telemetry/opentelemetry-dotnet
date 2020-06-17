// <copyright file="MetricExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics.Export
{
    public abstract class MetricExporter
    {
        public enum ExportResult
        {
            /// <summary>
            /// Batch is successfully exported.
            /// </summary>
            Success = 0,

            /// <summary>
            /// Batch export failed. Caller must not retry.
            /// </summary>
            FailedNotRetryable = 1,

            /// <summary>
            /// Batch export failed transiently. Caller should record error and may retry.
            /// </summary>
            FailedRetryable = 2,
        }

        public abstract Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken);
    }
}
