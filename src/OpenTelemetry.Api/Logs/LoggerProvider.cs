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

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// LoggerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Logger"/>.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
public
#else
/// <summary>
/// LoggerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Logger"/>.
/// </summary>
internal
#endif
    class LoggerProvider : BaseProvider
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
        => this.GetLogger(name: null, version: null);

    /// <summary>
    /// Gets a logger with the given name.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    public Logger GetLogger(string? name)
        => this.GetLogger(name, version: null);

    /// <summary>
    /// Gets a logger with the given name and version.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <param name="version">Optional version of the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    public Logger GetLogger(string? name, string? version)
    {
        if (!this.TryCreateLogger(name, out var logger))
        {
            return NoopLogger;
        }

        logger!.SetInstrumentationScope(version);

        return logger;
    }

    /// <summary>
    /// Try to create a logger with the given name.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <param name="logger"><see cref="Logger"/>.</param>
    /// <returns><see langword="true"/> if the logger was created.</returns>
    protected virtual bool TryCreateLogger(
        string? name,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        out Logger? logger)
    {
        logger = null;
        return false;
    }
}
