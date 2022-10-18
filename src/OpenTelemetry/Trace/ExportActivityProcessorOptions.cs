// <copyright file="ExportActivityProcessorOptions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Options for configuring either a <see cref="SimpleActivityExportProcessor"/> or <see cref="BatchActivityExportProcessor"/>.
/// </summary>
public class ExportActivityProcessorOptions
{
    private BatchExportActivityProcessorOptions batchExportProcessorOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportActivityProcessorOptions"/> class.
    /// </summary>
    public ExportActivityProcessorOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal ExportActivityProcessorOptions(IConfiguration configuration)
    {
        this.batchExportProcessorOptions = new BatchExportActivityProcessorOptions(configuration);
    }

    /// <summary>
    /// Gets or sets the export processor type to be used. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public ExportProcessorType ExportProcessorType { get; set; }

    /// <summary>
    /// Gets or sets the batch export options. Ignored unless <see cref="ExportProcessorType"/> is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public BatchExportActivityProcessorOptions BatchExportProcessorOptions
    {
        get => this.batchExportProcessorOptions;
        set
        {
            Guard.ThrowIfNull(value);

            this.batchExportProcessorOptions = value;
        }
    }
}
