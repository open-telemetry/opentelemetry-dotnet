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

using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Batch log processor options. OTEL_DOTNET_BLP_MAX_QUEUE_SIZE,
    /// OTEL_DOTNET_BLP_MAX_EXPORT_BATCH_SIZE, OTEL_DOTNET_BLP_EXPORT_TIMEOUT,
    /// OTEL_DOTNET_BLP_SCHEDULE_DELAY environment variables are parsed during
    /// object construction.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>The constructor throws <see cref="FormatException"/> if it fails
    /// to parse any of the supported environment variables.</item>
    /// <item>The environment variable keys are currently experimental and
    /// subject to change. See: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#opentelemetry-environment-variable-specification">OpenTelemetry
    /// Environment Variable Specification</see>.
    /// </item>
    /// </list>
    /// </remarks>
    public class BatchExportLogRecordProcessorOptions : BatchExportProcessorOptions<LogRecord>
    {
        internal const string MaxQueueSizeEnvVarKey = "OTEL_DOTNET_BLP_MAX_QUEUE_SIZE";

        internal const string MaxExportBatchSizeEnvVarKey = "OTEL_DOTNET_BLP_MAX_EXPORT_BATCH_SIZE";

        internal const string ExporterTimeoutEnvVarKey = "OTEL_DOTNET_BLP_EXPORT_TIMEOUT";

        internal const string ScheduledDelayEnvVarKey = "OTEL_DOTNET_BLP_SCHEDULE_DELAY";

        public BatchExportLogRecordProcessorOptions()
        {
            int value;

            if (EnvironmentVariableHelper.LoadNumeric(ExporterTimeoutEnvVarKey, out value))
            {
                this.ExporterTimeoutMilliseconds = value;
            }

            if (EnvironmentVariableHelper.LoadNumeric(MaxExportBatchSizeEnvVarKey, out value))
            {
                this.MaxExportBatchSize = value;
            }

            if (EnvironmentVariableHelper.LoadNumeric(MaxQueueSizeEnvVarKey, out value))
            {
                this.MaxQueueSize = value;
            }

            if (EnvironmentVariableHelper.LoadNumeric(ScheduledDelayEnvVarKey, out value))
            {
                this.ScheduledDelayMilliseconds = value;
            }
        }
    }
}
