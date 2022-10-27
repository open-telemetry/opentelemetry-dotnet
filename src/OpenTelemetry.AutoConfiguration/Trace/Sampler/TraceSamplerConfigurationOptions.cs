using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

public class TraceSamplerConfigurationOptions
{
    internal const string OTelTracesSamplerEnvVarKey = "OTEL_TRACES_SAMPLER";
    internal const string OTelTracesSamplerArgEnvVarKey = "OTEL_TRACES_SAMPLER_ARG";

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceSamplerConfigurationOptions"/> class.
    /// </summary>
    public TraceSamplerConfigurationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal TraceSamplerConfigurationOptions(IConfiguration configuration)
    {
        if (configuration.TryGetStringValue(OTelTracesSamplerEnvVarKey, out var value))
        {
            this.TraceSamplerName = value;
        }

        if (configuration.TryGetStringValue(OTelTracesSamplerArgEnvVarKey, out value))
        {
            this.TraceSamplerArgument = value;
        }
    }

    public string? TraceSamplerName { get; set; }

    public string? TraceSamplerArgument { get; set; }
}
