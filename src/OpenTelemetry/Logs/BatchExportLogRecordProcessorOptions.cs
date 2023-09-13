// <copyright file="BatchExportLogRecordProcessorOptions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Batch log processor options. OTEL_BLRP_MAX_QUEUE_SIZE,
/// OTEL_BLRP_MAX_EXPORT_BATCH_SIZE, OTEL_BLRP_EXPORT_TIMEOUT,
/// OTEL_BLRP_SCHEDULE_DELAY environment variables are parsed during object
/// construction.
/// </summary>
public class BatchExportLogRecordProcessorOptions : BatchExportProcessorOptions<LogRecord>
{
    internal const string MaxQueueSizeEnvVarKey = "OTEL_BLRP_MAX_QUEUE_SIZE";

    internal const string MaxExportBatchSizeEnvVarKey = "OTEL_BLRP_MAX_EXPORT_BATCH_SIZE";

    internal const string ExporterTimeoutEnvVarKey = "OTEL_BLRP_EXPORT_TIMEOUT";

    internal const string ScheduledDelayEnvVarKey = "OTEL_BLRP_SCHEDULE_DELAY";

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportLogRecordProcessorOptions"/> class.
    /// </summary>
    public BatchExportLogRecordProcessorOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal BatchExportLogRecordProcessorOptions(IConfiguration configuration)
    {
        if (configuration.TryGetIntValue(ExporterTimeoutEnvVarKey, out var value))
        {
            this.ExporterTimeoutMilliseconds = value;
        }

        if (configuration.TryGetIntValue(MaxExportBatchSizeEnvVarKey, out value))
        {
            this.MaxExportBatchSize = value;
        }

        if (configuration.TryGetIntValue(MaxQueueSizeEnvVarKey, out value))
        {
            this.MaxQueueSize = value;
        }

        if (configuration.TryGetIntValue(ScheduledDelayEnvVarKey, out value))
        {
            this.ScheduledDelayMilliseconds = value;
        }
    }
}
