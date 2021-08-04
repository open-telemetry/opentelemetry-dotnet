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
using System.Security;

namespace OpenTelemetry
{
    public class BatchExportProcessorOptions<T>
        where T : class
    {
        internal const string ScheduleDelayEnvVarName = "OTEL_BSP_SCHEDULE_DELAY";
        internal const string ExportTimeoutEnvVarName = "OTEL_BSP_EXPORT_TIMEOUT";
        internal const string MaxQueueSizeEnvVarName = "OTEL_BSP_MAX_QUEUE_SIZE";
        internal const string MaxExportBatchSizeEnvVarName = "OTEL_BSP_MAX_EXPORT_BATCH_SIZE";

        public BatchExportProcessorOptions()
        {
            try
            {
                var scheduleDelayEnvVar = Environment.GetEnvironmentVariable(ScheduleDelayEnvVarName);
                if (!string.IsNullOrEmpty(scheduleDelayEnvVar))
                {
                    if (int.TryParse(scheduleDelayEnvVar, out var scheduleDelay))
                    {
                        this.ScheduledDelayMilliseconds = scheduleDelay;
                    }
                    else
                    {
                        BatchExportProcessorEventSource.Log.FailedToParseEnvironmentVariable(ScheduleDelayEnvVarName, scheduleDelayEnvVar);
                    }
                }

                var exportTimeoutEnvVar = Environment.GetEnvironmentVariable(ExportTimeoutEnvVarName);
                if (!string.IsNullOrEmpty(exportTimeoutEnvVar))
                {
                    if (int.TryParse(exportTimeoutEnvVar, out var exportTimeout))
                    {
                        this.ExporterTimeoutMilliseconds = exportTimeout;
                    }
                    else
                    {
                        BatchExportProcessorEventSource.Log.FailedToParseEnvironmentVariable(ExportTimeoutEnvVarName, exportTimeoutEnvVar);
                    }
                }

                var maxQueueSizeEnvVar = Environment.GetEnvironmentVariable(MaxQueueSizeEnvVarName);
                if (!string.IsNullOrEmpty(maxQueueSizeEnvVar))
                {
                    if (int.TryParse(maxQueueSizeEnvVar, out var maxQueueSize))
                    {
                        this.MaxQueueSize = maxQueueSize;
                    }
                    else
                    {
                        BatchExportProcessorEventSource.Log.FailedToParseEnvironmentVariable(MaxQueueSizeEnvVarName, maxQueueSizeEnvVar);
                    }
                }

                var maxExportBatchSizeEnvVar = Environment.GetEnvironmentVariable(MaxExportBatchSizeEnvVarName);
                if (!string.IsNullOrEmpty(maxExportBatchSizeEnvVar))
                {
                    if (int.TryParse(maxExportBatchSizeEnvVar, out var maxExportBatchSize))
                    {
                        this.MaxExportBatchSize = maxExportBatchSize;
                    }
                    else
                    {
                        BatchExportProcessorEventSource.Log.FailedToParseEnvironmentVariable(MaxExportBatchSizeEnvVarName, maxExportBatchSizeEnvVar);
                    }
                }
            }
            catch (SecurityException ex)
            {
                // The caller does not have the required permission to
                // retrieve the value of an environment variable from the current process.
                BatchExportProcessorEventSource.Log.MissingPermissionsToReadEnvironmentVariable(ex);
            }
        }

        /// <summary>
        /// Gets or sets the maximum queue size. The queue drops the data if the maximum size is reached. The default value is 2048.
        /// </summary>
        public int MaxQueueSize { get; set; } = BatchExportProcessor<T>.DefaultMaxQueueSize;

        /// <summary>
        /// Gets or sets the delay interval (in milliseconds) between two consecutive exports. The default value is 5000.
        /// </summary>
        public int ScheduledDelayMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds;

        /// <summary>
        /// Gets or sets the timeout (in milliseconds) after which the export is cancelled. The default value is 30000.
        /// </summary>
        public int ExporterTimeoutMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds;

        /// <summary>
        /// Gets or sets the maximum batch size of every export. It must be smaller or equal to MaxQueueLength. The default value is 512.
        /// </summary>
        public int MaxExportBatchSize { get; set; } = BatchExportProcessor<T>.DefaultMaxExportBatchSize;
    }
}
