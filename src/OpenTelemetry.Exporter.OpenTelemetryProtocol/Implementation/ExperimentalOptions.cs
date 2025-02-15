// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ExperimentalOptions
{
    public const string LogRecordEventIdAttribute = "logrecord.event.id";

    public const string LogRecordEventNameAttribute = "logrecord.event.name";

    public const string EmitLogEventEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES";

    public const string OtlpRetryEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY";

    public const string OtlpDiskRetryDirectoryPathEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH";

    public ExperimentalOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    public ExperimentalOptions(IConfiguration configuration)
    {
        if (configuration.TryGetBoolValue(OpenTelemetryProtocolExporterEventSource.Log, EmitLogEventEnvVar, out var emitLogEventAttributes))
        {
            this.EmitLogEventAttributes = emitLogEventAttributes;
        }

        if (configuration.TryGetStringValue(OtlpRetryEnvVar, out var retryPolicy))
        {
            if (retryPolicy.Equals("in_memory", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableInMemoryRetry = true;
            }
            else if (retryPolicy.Equals("disk", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableDiskRetry = true;
                if (configuration.TryGetStringValue(OtlpDiskRetryDirectoryPathEnvVar, out var path))
                {
                    this.DiskRetryDirectoryPath = path;
                }
                else
                {
                    // Fallback to temp location.
                    this.DiskRetryDirectoryPath = Path.GetTempPath();
                }
            }
            else
            {
                throw new NotSupportedException($"Retry Policy '{retryPolicy}' is not supported.");
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether log event attributes should be exported.
    /// </summary>
    public bool EmitLogEventAttributes { get; }

    /// <summary>
    /// Gets a value indicating whether or not in-memory retry should be enabled for transient errors.
    /// </summary>
    /// <remarks>
    /// Specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#retry"/>.
    /// </remarks>
    public bool EnableInMemoryRetry { get; }

    /// <summary>
    /// Gets a value indicating whether or not retry via disk should be enabled for transient errors.
    /// </summary>
    public bool EnableDiskRetry { get; }

    /// <summary>
    /// Gets the path on disk where the telemetry will be stored for retries at a later point.
    /// </summary>
    public string? DiskRetryDirectoryPath { get; }
}
