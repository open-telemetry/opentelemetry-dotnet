// <copyright file="OtlpTraceExporterPersistentStorageExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.PersistentStorage.Abstractions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter
/// with persistent storage.
/// </summary>
public static class OtlpTraceExporterPersistentStorageExtensions
{
    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider
    /// with access to persistent storage.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <param name="persistentStorageFactory">Factory function to create a <see cref="PersistentBlobProvider"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporterWithPersistentStorage(
        this TracerProviderBuilder builder,
        string? name,
        Action<OtlpExporterOptions> configure,
        Func<IServiceProvider, PersistentBlobProvider> persistentStorageFactory)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(persistentStorageFactory);

        builder.ConfigureServices(services =>
        {
            services
                  .AddOptions<OtlpExporterOptions>(name)
                  .Configure<IServiceProvider>((otlpExporterOptions, serviceProvider) =>
                  {
                      otlpExporterOptions.PersistentBlobProvider = persistentStorageFactory(serviceProvider);
                  });
        });

        return builder.AddOtlpExporter(name, configure);
    }

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider
    /// with access to persistent storage.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <param name="persistentStorageFactory">Factory function to create a <see cref="PersistentBlobProvider"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporterWithPersistentStorage(
        this TracerProviderBuilder builder,
        Action<OtlpExporterOptions> configure,
        Func<IServiceProvider, PersistentBlobProvider> persistentStorageFactory)
    {
        return builder.AddOtlpExporterWithPersistentStorage(name: null, configure, persistentStorageFactory);
    }
}
