// <copyright file="OpenTelemetrySerilogExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using Serilog.Configuration;

namespace Serilog
{
    /// <summary>
    /// Contains Serilog extension methods.
    /// </summary>
    public static class OpenTelemetrySerilogExtensions
    {
        /// <summary>
        /// Adds a sink to Serilog <see cref="LoggerConfiguration"/> which will
        /// write to OpenTelemetry.
        /// </summary>
        /// <param name="loggerConfiguration"><see
        /// cref="LoggerSinkConfiguration"/>.</param>
        /// <param name="openTelemetryLoggerProvider"><see
        /// cref="OpenTelemetryLoggerProvider"/>.</param>
        /// <param name="disposeProvider">Controls whether or not the supplied
        /// <paramref name="openTelemetryLoggerProvider"/> will be disposed when
        /// the logger is disposed. Default value: <see
        /// langword="true"/>.</param>
        /// <returns>Supplied <see cref="LoggerConfiguration"/> for chaining calls.</returns>
        public static LoggerConfiguration OpenTelemetry(
            this LoggerSinkConfiguration loggerConfiguration,
            OpenTelemetryLoggerProvider openTelemetryLoggerProvider,
            bool disposeProvider = true)
        {
            Guard.ThrowIfNull(loggerConfiguration);
            Guard.ThrowIfNull(openTelemetryLoggerProvider);

#pragma warning disable CA2000 // Dispose objects before losing scope
            return loggerConfiguration.Sink(new OpenTelemetrySerilogSink(openTelemetryLoggerProvider, disposeProvider));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
    }
}
