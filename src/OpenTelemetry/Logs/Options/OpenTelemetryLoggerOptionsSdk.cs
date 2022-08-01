// <copyright file="OpenTelemetryLoggerOptionsSdk.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetryLoggerOptionsSdk : OpenTelemetryLoggerOptions
{
    public OpenTelemetryLoggerOptionsSdk(Action<OpenTelemetryLoggerOptions>? configure)
    {
        var services = new ServiceCollection();

        services.AddOptions();

        this.Services = services;

        configure?.Invoke(this);
    }

    public OpenTelemetryLoggerProvider Build()
    {
        var services = this.Services;

        if (services == null)
        {
            throw new NotSupportedException("LoggerProviderBuilder build method cannot be called multiple times.");
        }

        this.Services = null;

        var serviceProvider = services.BuildServiceProvider();

        var finalOptions = serviceProvider.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

        this.ApplyTo(finalOptions);

        return new OpenTelemetryLoggerProvider(
            finalOptions,
            serviceProvider,
            ownsServiceProvider: true);
    }
}
