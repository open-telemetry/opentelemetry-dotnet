// <copyright file="ConsoleExporterMetricsExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the Console exporter.
/// </summary>
public static class ConsoleExporterMetricsExtensions
{
    private const int DefaultExportIntervalMilliseconds = 10000;
    private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/> using default options.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder)
        => AddConsoleExporter(builder, name: null, configureExporter: null);

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder, Action<ConsoleExporterOptions> configureExporter)
        => AddConsoleExporter(builder, name: null, configureExporter);

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        string name,
        Action<ConsoleExporterOptions> configureExporter)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configureExporter != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureExporter));
        }

        return builder.AddReader(sp =>
        {
            return BuildConsoleExporterMetricReader(
                sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name),
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name));
        });
    }

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporterAndMetricReader">Callback action for
    /// configuring <see cref="ConsoleExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        Action<ConsoleExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
        => AddConsoleExporter(builder, name: null, configureExporterAndMetricReader);

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureExporterAndMetricReader">Callback action for
    /// configuring <see cref="ConsoleExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        string name,
        Action<ConsoleExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        return builder.AddReader(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);
            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildConsoleExporterMetricReader(exporterOptions, metricReaderOptions);
        });
    }

    private static MetricReader BuildConsoleExporterMetricReader(
        ConsoleExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions)
    {
        var metricExporter = new ConsoleMetricExporter(exporterOptions);

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions,
            DefaultExportIntervalMilliseconds,
            DefaultExportTimeoutMilliseconds);
    }
}
