// <copyright file="OpenTelemetryBuilderHostingExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Contains hosting extension methods for the <see
/// cref="OpenTelemetryBuilder"/> class.
/// </summary>
public static class OpenTelemetryBuilderHostingExtensions
{
    /// <summary>
    /// Registers an <see cref="IHostedService"/> to automatically start all
    /// configured OpenTelemetry services in the supplied <see
    /// cref="IServiceCollection" />.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times. Only a single <see
    /// cref="IHostedService"/> will be created for a given <see
    /// cref="IServiceCollection"/>. This should generally be called by hosting
    /// application code and NOT library authors.
    /// </remarks>
    /// <param name="builder"><see cref="OpenTelemetryBuilder"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static OpenTelemetryBuilder StartWithHost(this OpenTelemetryBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());

        return builder;
    }
}
