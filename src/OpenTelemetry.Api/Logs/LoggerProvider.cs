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
internal class LoggerProvider : BaseProvider
{
    private static readonly NoopLogger NoopLogger = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProvider"/> class.
    /// </summary>
    protected LoggerProvider()
    {
    }

    /// <summary>
    /// Gets a logger.
    /// </summary>
    /// <returns><see cref="Logger"/> instance.</returns>
    public Logger GetLogger()
        => this.GetLogger(new InstrumentationScope());

    /// <summary>
    /// Gets a logger with the given name.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    public Logger GetLogger(string name)
        => this.GetLogger(new InstrumentationScope(name));

    /// <summary>
    /// Gets a logger with given instrumentation scope.
    /// </summary>
    /// <param name="instrumentationScope"><see cref="InstrumentationScope"/>.</param>
    /// <returns><see cref="Logger"/>.</returns>
    public virtual Logger GetLogger(InstrumentationScope instrumentationScope)
        => NoopLogger;
}
