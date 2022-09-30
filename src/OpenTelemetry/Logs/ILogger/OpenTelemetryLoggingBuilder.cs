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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs;

/// <summary>
/// An <see cref="ILoggingBuilder"/> implementation that exposes methods for configuring OpenTelemetry.
/// </summary>
public sealed class OpenTelemetryLoggingBuilder : LoggerProviderBuilder, ILoggingBuilder
{
    internal OpenTelemetryLoggingBuilder(IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null");

        this.InnerBuiler = new LoggerProviderBuilderSdk(services!);
        this.Services = services!;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    internal LoggerProviderBuilderSdk InnerBuiler { get; }
}
