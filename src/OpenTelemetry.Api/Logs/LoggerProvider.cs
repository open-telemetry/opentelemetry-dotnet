// <copyright file="LoggerProvider.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// LoggerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Logger"/>.
/// </summary>
public class LoggerProvider : BaseProvider
{
    private NoopLogger? noopLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProvider"/> class.
    /// </summary>
    protected LoggerProvider()
    {
    }

    internal Logger GetLogger()
        => this.GetLogger(name: null);

    internal Logger GetLogger(string? name)
    => this.GetLogger(new LoggerOptions(name));

    internal Logger GetLogger(InstrumentationScope instrumentationScope)
        => this.GetLogger(new LoggerOptions(instrumentationScope));

    /// <summary>
    /// Gets a logger with the given options.
    /// </summary>
    /// <param name="options">Optional <see cref="LoggerOptions"/>.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    internal virtual Logger GetLogger(LoggerOptions options)
    {
        return this.noopLogger ??= new();
    }
}
