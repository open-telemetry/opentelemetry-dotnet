// <copyright file="LogRecordExportProcessorOptions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Options for configuring either a <see cref="SimpleLogRecordExportProcessor"/> or <see cref="BatchLogRecordExportProcessor"/>.
/// </summary>
public class LogRecordExportProcessorOptions
{
    private BatchExportLogRecordProcessorOptions batchExportProcessorOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordExportProcessorOptions"/> class.
    /// </summary>
    public LogRecordExportProcessorOptions()
        : this(new())
    {
    }

    internal LogRecordExportProcessorOptions(
        BatchExportLogRecordProcessorOptions defaultBatchExportLogRecordProcessorOptions)
    {
        Debug.Assert(defaultBatchExportLogRecordProcessorOptions != null, "defaultBatchExportLogRecordProcessorOptions was null");

        this.batchExportProcessorOptions = defaultBatchExportLogRecordProcessorOptions;
    }

    /// <summary>
    /// Gets or sets the export processor type to be used. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    /// <summary>
    /// Gets or sets the batch export options. Ignored unless <see cref="ExportProcessorType"/> is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public BatchExportLogRecordProcessorOptions BatchExportProcessorOptions
    {
        get => this.batchExportProcessorOptions;
        set
        {
            Guard.ThrowIfNull(value);
            this.batchExportProcessorOptions = value;
        }
    }

    internal static void RegisterLogRecordExportProcessorOptionsFactory(IServiceCollection services)
    {
        // Note: This registers a factory so that when
        // sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(name)))
        // is executed the SDK internal
        // BatchExportLogRecordProcessorOptions(IConfiguration) ctor is used
        // correctly which allows users to control the OTEL_BLRP_* keys using
        // IConfiguration (envvars, appSettings, cli, etc.).

        services.RegisterOptionsFactory(
            (sp, configuration, name) => new LogRecordExportProcessorOptions(
                sp.GetRequiredService<IOptionsMonitor<BatchExportLogRecordProcessorOptions>>().Get(name)));
    }
}
