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

using System.Diagnostics;

namespace OpenTelemetry;

/// <summary>
/// Implements processor that batches <see cref="Activity"/> objects before calling exporter.
/// </summary>
public class BatchActivityExportProcessor : BatchExportProcessor<Activity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BatchActivityExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor" path="/param[@name='exporter']"/></param>
    /// <param name="maxQueueSize"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor" path="/param[@name='maxQueueSize']"/></param>
    /// <param name="scheduledDelayMilliseconds"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor" path="/param[@name='scheduledDelayMilliseconds']"/></param>
    /// <param name="exporterTimeoutMilliseconds"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor" path="/param[@name='exporterTimeoutMilliseconds']"/></param>
    /// <param name="maxExportBatchSize"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor" path="/param[@name='maxExportBatchSize']"/></param>
    public BatchActivityExportProcessor(
        BaseExporter<Activity> exporter,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(
            exporter,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
    {
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (!data.Recorded)
        {
            return;
        }

        this.OnExport(data);
    }
}
