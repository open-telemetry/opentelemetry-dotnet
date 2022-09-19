// <copyright file="OpenTelemetryLoggerOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Contains options that apply to log messages written through the
    /// OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    public class OpenTelemetryLoggerOptions
    {
        internal readonly List<BaseProcessor<LogRecord>> Processors = new();
        internal ResourceBuilder? ResourceBuilder;

        /// <summary>
        /// Gets or sets a value indicating whether or not log attributes should
        /// be included on generated <see cref="LogRecord"/>s. Default value:
        /// <see langword="true"/>.
        /// </summary>
        public bool IncludeAttributes { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not formatted log message
        /// should be included on generated <see cref="LogRecord"/>s. Default
        /// value: <see langword="false"/>.
        /// </summary>
        public bool IncludeFormattedMessage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not log scopes should be
        /// included on generated <see cref="LogRecord"/>s. Default value:
        /// <see langword="false"/>.
        /// </summary>
        public bool IncludeScopes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see
        /// cref="Activity.TraceStateString"/> for the current <see
        /// cref="Activity"/> should be included on generated <see
        /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
        /// </summary>
        public bool IncludeTraceState { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not log state should be
        /// parsed into <see cref="LogRecord.Attributes"/> on generated <see
        /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item>Parsing is only executed when the state logged does NOT
        /// implement <see cref="IReadOnlyList{T}"/> or <see
        /// cref="IEnumerable{T}"/> where <c>T</c> is <c>KeyValuePair&lt;string,
        /// object&gt;</c>.</item>
        /// <item>When <see cref="ParseStateValues"/> is set to <see
        /// langword="true"/> <see cref="LogRecord.State"/> will always be <see
        /// langword="null"/>.</item>
        /// </list>
        /// </remarks>
        public bool ParseStateValues { get; set; }

        /// <summary>
        /// Adds a processor to the provider.
        /// </summary>
        /// <param name="processor">LogRecord processor to add.</param>
        /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
        [Obsolete("Use LoggerProviderBuilder instead of OpenTelemetryLoggerOptions to configure a LoggerProvider this method will be removed in a future version.")]
        public OpenTelemetryLoggerOptions AddProcessor(BaseProcessor<LogRecord> processor)
        {
            Guard.ThrowIfNull(processor);

            this.Processors.Add(processor);
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// You should usually use <see cref="LoggerProviderBuilderExtensions.ConfigureResource(LoggerProviderBuilder, Action{ResourceBuilder})"/> instead
        /// (call <see cref="ResourceBuilder.Clear"/> if desired).
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
        [Obsolete("Use LoggerProviderBuilder instead of OpenTelemetryLoggerOptions to configure a LoggerProvider this method will be removed in a future version.")]
        public OpenTelemetryLoggerOptions SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Guard.ThrowIfNull(resourceBuilder);

            this.ResourceBuilder = resourceBuilder;
            return this;
        }
    }
}
