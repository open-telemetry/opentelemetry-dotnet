// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Reflection;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

/// <summary>
/// OpenTelemetry Protocol (OTLP) exporter options.
/// OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, OTEL_EXPORTER_OTLP_TIMEOUT, OTEL_EXPORTER_OTLP_PROTOCOL
/// environment variables are parsed during object construction.
/// </summary>
public class OtlpExporterOptions : OtlpExporterOptionsBase
{
    internal static readonly KeyValuePair<string, string>[] StandardHeaders = new KeyValuePair<string, string>[]
    {
        new KeyValuePair<string, string>("User-Agent", GetUserAgentString()),
    };

    private const string UserAgentProduct = "OTel-OTLP-Exporter-Dotnet";

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
    /// </summary>
    public OtlpExporterOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build(), new())
    {
    }

    internal OtlpExporterOptions(
        IConfiguration configuration,
        BatchExportActivityProcessorOptions defaultBatchOptions)
        : base(configuration, OtlpExporterSignals.None)
    {
        Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");

        this.BatchExportProcessorOptions = defaultBatchOptions;
    }

    /// <summary>
    /// Gets or sets the export processor type to be used with the OpenTelemetry Protocol Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    /// <remarks>Note: This only applies when exporting traces.</remarks>
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    /// <summary>
    /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is Batch.
    /// </summary>
    /// <remarks>Note: This only applies when exporting traces.</remarks>
    public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; }

    internal static void RegisterOtlpExporterOptionsFactory(IServiceCollection services)
    {
        services.RegisterOptionsFactory(CreateOtlpExporterOptions);
    }

    internal static OtlpExporterOptions CreateOtlpExporterOptions(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string name)
        => new(
            configuration,
            serviceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name));

    private static string GetUserAgentString()
    {
        try
        {
            var assemblyVersion = typeof(OtlpExporterOptions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var informationalVersion = assemblyVersion?.InformationalVersion;
            return string.IsNullOrEmpty(informationalVersion) ? UserAgentProduct : $"{UserAgentProduct}/{informationalVersion}";
        }
        catch (Exception)
        {
            return UserAgentProduct;
        }
    }
}
