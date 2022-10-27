using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

public class TraceExporterConfigurationOptions
{
    internal const string OTelTracesExporterEnvVarKey = "OTEL_TRACES_EXPORTER";

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceExporterConfigurationOptions"/> class.
    /// </summary>
    public TraceExporterConfigurationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal TraceExporterConfigurationOptions(IConfiguration configuration)
    {
        if (configuration.TryGetStringValue(OTelTracesExporterEnvVarKey, out var value))
        {
            this.TraceExporterName = value;
        }
    }

    public string? TraceExporterName { get; set; }
}
