// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ExperimentalOptions
{
    public const string LogRecordEventIdAttribute = "logrecord.event.id";

    public const string EmitLogEventEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES";

    public const string OtlpRetryEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY";

    public const string OtlpDiskRetryDirectoryPathEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH";

    public const string OtlpDiskRetryMaxSizeInMbEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_MAX_SIZE_IN_MB";

    public const int DefaultDiskRetryMaxSizeInMb = 50;

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

                this.DiskRetryMaxSizeInBytes = configuration.TryGetStringValue(OtlpDiskRetryMaxSizeInMbEnvVar, out var maxSizeInMb)
                    && long.TryParse(maxSizeInMb, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxSizeInMb)
                    ? parsedMaxSizeInMb * 1024 * 1024
                    : (long)DefaultDiskRetryMaxSizeInMb * 1024 * 1024;
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

    /// <summary>
    /// Gets the maximum allowed size in bytes for the disk retry storage folder.
    /// </summary>
    public long DiskRetryMaxSizeInBytes { get; }
}
