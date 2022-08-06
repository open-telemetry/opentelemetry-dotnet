// <copyright file="OpenTelemetryLoggerOptionsExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods for the <see cref="OpenTelemetryLoggerOptions"/> class.
/// </summary>
public static class OpenTelemetryLoggerOptionsExtensions
{
    /// <summary>
    /// Run the given actions to initialize the <see cref="OpenTelemetryLoggerProvider"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <returns><see cref="OpenTelemetryLoggerProvider"/>.</returns>
    public static OpenTelemetryLoggerProvider Build(this OpenTelemetryLoggerOptions options)
    {
        Guard.ThrowIfNull(options);

        if (options is not OpenTelemetryLoggerOptionsSdk openTelemetryLoggerOptionsSdk)
        {
            throw new NotSupportedException("Build is only supported on options instances created using the Sdk.CreateLoggerProviderBuilder method.");
        }

        return openTelemetryLoggerOptionsSdk.Build();
    }
}
