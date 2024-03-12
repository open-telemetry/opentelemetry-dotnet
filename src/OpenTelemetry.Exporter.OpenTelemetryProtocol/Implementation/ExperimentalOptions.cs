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

    public const string EnableInMemoryRetryEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_ENABLE_RETRIES";

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
    public bool EmitLogEventAttributes { get; private set; } = false;

    /// <summary>
    /// Gets a value indicating whether retries should be enabled in case of transient errors.
    /// </summary>
    public bool EnableInMemoryRetry { get; private set; } = false;
}
