// <copyright file="ExportProcessorType.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    /// <summary>
    /// Type of Export Processor to be used.
    /// </summary>
    public enum ExportProcessorType
    {
        /// <summary>
        /// Use SimpleExportProcessor.
        /// Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#simple-processor">
        /// specification</a> for more information.
        /// </summary>
        Simple,

        /// <summary>
        /// Use BatchExportProcessor.
        /// Refer to <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#batching-processor">
        /// specification</a> for more information.
        /// </summary>
        Batch,
    }
}
