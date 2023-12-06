// <copyright file="OpenTelemetrySdkExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Contains methods for extending the <see cref="OpenTelemetrySdk"/> class.
/// </summary>
public static class OpenTelemetrySdkExtensions
#else
/// <summary>
/// Contains methods for extending the <see cref="OpenTelemetrySdk"/> class.
/// </summary>
internal static class OpenTelemetrySdkExtensions
#endif
{
    private static readonly NullLoggerFactory NoopLoggerFactory = new();

    /// <summary>
    /// Gets the <see cref="ILoggerFactory"/> contained in an <see cref="OpenTelemetrySdk"/> instance.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="ILoggerFactory"/> will be a no-op instance.
    /// Call <see cref="OpenTelemetryBuilder.WithLogging()"/> or <see
    /// cref="OpenTelemetryBuilder.WithLogging(Action{LoggerProviderBuilder})"/>
    /// to enable logging.
    /// </remarks>
    /// <param name="sdk"><see cref="OpenTelemetrySdk"/>.</param>
    /// <returns><see cref="ILoggerFactory"/>.</returns>
    public static ILoggerFactory GetLoggerFactory(this OpenTelemetrySdk sdk)
    {
        Guard.ThrowIfNull(sdk);

        return (ILoggerFactory?)sdk.Services.GetService(typeof(ILoggerFactory))
            ?? NoopLoggerFactory;
    }
}
