// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ExperimentalOptions
{
    public const string LogRecordEventIdAttribute = "logrecord.event.id";

    public const string LogRecordEventNameAttribute = "logrecord.event.name";

    public const string EmitLogEventEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES";

    public const string EnableInMemoryRetryEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_ENABLE_INMEMORY_RETRY";

    public ExperimentalOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    public ExperimentalOptions(IConfiguration configuration)
    {
        if (configuration.TryGetBoolValue(EmitLogEventEnvVar, out var emitLogEventAttributes))
        {
            this.EmitLogEventAttributes = emitLogEventAttributes;
        }

        if (configuration.TryGetBoolValue(EnableInMemoryRetryEnvVar, out var enableInMemoryRetry))
        {
            this.EnableInMemoryRetry = enableInMemoryRetry;
        }
    }

    /// <summary>
    /// Gets a value indicating whether log event attributes should be exported.
    /// </summary>
    public bool EmitLogEventAttributes { get; }

    /// <summary>
    /// Gets a value indicating whether retries should be enabled in case of transient errors.
    /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#retry
    /// </summary>
    public bool EnableInMemoryRetry { get; }
}
