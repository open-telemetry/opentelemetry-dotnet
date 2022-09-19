// <copyright file="OpenTelemetryLoggingBuilder.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// An <see cref="ILoggingBuilder"/> implementation that exposes methods for configuring OpenTelemetry.
/// </summary>
public sealed class OpenTelemetryLoggingBuilder : ILoggingBuilder
{
    internal OpenTelemetryLoggingBuilder(IServiceCollection services)
    {
        this.Services = services;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="LoggerProviderBuilder"/> in the <see cref="IServiceCollection"/>
    /// where the OpenTelemetry <see cref="ILoggerProvider"/> will be created.
    /// </summary>
    /// <param name="configure">Callback action to configure the <see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <returns>Supplied <see cref="OpenTelemetryLoggingBuilder"/> instance for chaining calls.</returns>
    public OpenTelemetryLoggingBuilder WithConfiguration(Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        var builder = new LoggerProviderBuilderSdk(this.Services);

        configure(builder);

        return this;
    }
}
