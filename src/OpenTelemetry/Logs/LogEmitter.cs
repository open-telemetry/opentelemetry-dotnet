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
    public sealed class LogEmitter
    {
        private readonly OpenTelemetryLoggerProvider loggerProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEmitter"/> class.
        /// </summary>
        /// <param name="loggerProvider"><see cref="OpenTelemetryLoggerProvider"/>.</param>
        public LogEmitter(OpenTelemetryLoggerProvider loggerProvider)
        {
            Guard.ThrowIfNull(loggerProvider);

            this.loggerProvider = loggerProvider;
        }

        /// <summary>
        /// Emit a <see cref="LogRecord"/>.
        /// </summary>
        /// <param name="logRecord"><see cref="LogRecord"/>.</param>
        public void Log(LogRecord logRecord)
        {
            Guard.ThrowIfNull(logRecord);

            var provider = this.loggerProvider;
            var processor = provider.Processor;
            if (processor != null)
            {
                if (provider.IncludeScopes)
                {
                    logRecord.ScopeProvider = provider.ScopeProvider;
                }

                processor.OnEnd(logRecord);

                logRecord.ScopeProvider = null;
            }
        }
    }
}
