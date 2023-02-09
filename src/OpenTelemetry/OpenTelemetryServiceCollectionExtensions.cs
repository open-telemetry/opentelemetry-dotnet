// <copyright file="OpenTelemetryServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Contains <see cref="IServiceCollection"/> extension methods for registering OpenTelemetry SDK artifacts.
/// </summary>
public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenTelemetry SDK services into the supplied <see
    /// cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>A <see cref="TracerProvider"/> and/or <see cref="MeterProvider"/>
    /// will not be created automatically using this method. To begin collecting
    /// traces and/or metrics either use the
    /// <c>OpenTelemetryBuilder.StartWithHost</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package or access the <see
    /// cref="TracerProvider"/> and/or <see cref="MeterProvider"/> through the
    /// application <see cref="IServiceProvider"/>.</item>
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> and/or <see
    /// cref="MeterProvider"/> will be created for a given <see
    /// cref="IServiceCollection"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static OpenTelemetryBuilder AddOpenTelemetry(this IServiceCollection services)
        => new(services);
}
