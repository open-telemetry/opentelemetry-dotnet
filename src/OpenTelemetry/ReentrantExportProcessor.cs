// <copyright file="ReentrantExportProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry
{
    /// <summary>
    /// Implements processor that exports telemetry data at each OnEnd call without synchronization.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public class ReentrantExportProcessor<T> : BaseExportProcessor<T>
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReentrantExportProcessor{T}"/> class.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        public ReentrantExportProcessor(BaseExporter<T> exporter)
            : base(exporter)
        {
        }

        /// <inheritdoc />
        public override void OnEnd(T data)
        {
            try
            {
                this.exporter.Export(new Batch<T>(data));
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.OnEnd), ex);
            }
        }
    }
}
