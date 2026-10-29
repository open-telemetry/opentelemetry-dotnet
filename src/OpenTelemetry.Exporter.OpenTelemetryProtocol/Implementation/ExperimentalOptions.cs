// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ExperimentalOptions
{
    public const string LogRecordEventIdAttribute = "logrecord.event.id";

    public const string EmitLogEventEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES";

    public const string OtlpRetryEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY";

    public const string OtlpDiskRetryDirectoryPathEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH";

    public const string Utf8SpansEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_UTF8_SPANS";

    public ExperimentalOptions()
        : this(EnvironmentVariablesConfiguration.Create())
    {
    }

    public ExperimentalOptions(IConfiguration configuration)
    {
        if (configuration.TryGetBoolValue(OpenTelemetryProtocolExporterEventSource.Log, EmitLogEventEnvVar, out var emitLogEventAttributes))
        {
            this.EmitLogEventAttributes = emitLogEventAttributes;
        }

        if (configuration.TryGetBoolValue(OpenTelemetryProtocolExporterEventSource.Log, Utf8SpansEnvVar, out var enableUtf8Spans))
        {
            this.EnableUtf8Spans = enableUtf8Spans;
        }

        if (configuration.TryGetStringValue(OtlpRetryEnvVar, out var retryPolicy))
        {
            if (string.Equals(retryPolicy, "in_memory", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableInMemoryRetry = true;
            }
            else if (string.Equals(retryPolicy, "disk", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableDiskRetry = true;

                this.DiskRetryDirectoryPath = configuration.TryGetStringValue(OtlpDiskRetryDirectoryPathEnvVar, out var path)
                    ? path
                    : throw new NotSupportedException(
                        $"Retry Policy '{retryPolicy}' requires '{OtlpDiskRetryDirectoryPathEnvVar}' to be configured.");
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
    /// Gets a value indicating whether attribute values implementing <c>IUtf8SpanFormattable</c>
    /// are formatted directly into the export buffer rather than through <c>ToString()</c>.
    /// </summary>
    public bool EnableUtf8Spans { get; }

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
