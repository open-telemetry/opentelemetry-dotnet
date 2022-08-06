// <copyright file="ExportProcessorOptions.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

public class ExportProcessorOptions
{
    private readonly bool isReadonly;
    private ExportProcessorType exportProcessorType = ExportProcessorType.Batch;
    private BatchExportProcessorOptions<Activity> batchExportProcessorOptions = new();

    public ExportProcessorOptions()
    {
    }

    internal ExportProcessorOptions(ExportProcessorType exportProcessorType)
    {
        this.ExportProcessorType = exportProcessorType;
        this.isReadonly = true;
    }

    public static ExportProcessorOptions Simple { get; } = new(ExportProcessorType.Simple);

    public static ExportProcessorOptions Batch { get; } = new(ExportProcessorType.Batch);

    /// <summary>
    /// Gets or sets the export processor type to be used. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public ExportProcessorType ExportProcessorType
    {
        get => this.exportProcessorType;
        set
        {
            if (this.isReadonly)
            {
                throw new NotSupportedException();
            }

            this.exportProcessorType = value;
        }
    }

    /// <summary>
    /// Gets or sets the batch export options. Ignored unless <see cref="ExportProcessorType"/> is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions
    {
        get => this.batchExportProcessorOptions;
        set
        {
            if (this.isReadonly)
            {
                throw new NotSupportedException();
            }

            Guard.ThrowIfNull(value);

            this.batchExportProcessorOptions = value;
        }
    }
}
