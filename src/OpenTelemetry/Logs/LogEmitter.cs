// <copyright file="LogEmitter.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// LogEmitter implementation.
    /// </summary>
    /// <remarks>
    /// Spec reference: <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/logging-library-sdk.md#logemitter">LogEmitter</a>.
    /// </remarks>
    internal sealed class LogEmitter
    {
        private readonly OpenTelemetryLoggerProvider loggerProvider;

        internal LogEmitter(OpenTelemetryLoggerProvider loggerProvider)
        {
            Guard.ThrowIfNull(loggerProvider);

            this.loggerProvider = loggerProvider;
        }

        /// <summary>
        /// Emit a <see cref="LogRecord"/>.
        /// </summary>
        /// <param name="data"><see cref="LogRecordData"/>.</param>
        /// <param name="attributes"><see cref="LogRecordAttributeList"/>.</param>
        public void Log(in LogRecordData data, in LogRecordAttributeList attributes = default)
        {
            var provider = this.loggerProvider;
            var processor = provider.Processor;
            if (processor != null)
            {
                var pool = provider.LogRecordPool;

                var logRecord = pool.Rent();

                logRecord.Data = data;

                attributes.ApplyToLogRecord(logRecord);

                processor.OnEnd(logRecord);

                // Attempt to return the LogRecord to the pool. This will no-op
                // if a batch exporter has added a reference.
                pool.Return(logRecord);
            }
        }
    }
}
