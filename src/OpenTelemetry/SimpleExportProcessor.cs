// <copyright file="SimpleExportProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    /// <summary>
    /// Implements processor that exports telemetry data at each OnEnd call.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public abstract class SimpleExportProcessor<T> : BaseExportProcessor<T>
        where T : class
    {
        private readonly object syncObject = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleExportProcessor{T}"/> class.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        protected SimpleExportProcessor(BaseExporter<T> exporter)
            : base(exporter)
        {
        }

        /// <inheritdoc />
        protected override void OnExport(T data)
        {
            lock (this.syncObject)
            {
                try
                {
                    this.exporter.Export(new Batch<T>(data));
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.OnExport), ex);
                }
            }
        }
    }
}
