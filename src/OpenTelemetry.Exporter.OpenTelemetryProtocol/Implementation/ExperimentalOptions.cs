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

    public const string EmitLogExceptionEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES";

    public const string EmitLogEventEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES";

    public ExperimentalOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    public ExperimentalOptions(IConfiguration configuration)
    {
        if (configuration.TryGetBoolValue(EmitLogExceptionEnvVar, out var emitLogExceptionAttributes))
        {
            this.EmitLogExceptionAttributes = emitLogExceptionAttributes;
        }

        if (configuration.TryGetBoolValue(EmitLogEventEnvVar, out var emitLogEventAttributes))
        {
            this.EmitLogEventAttributes = emitLogEventAttributes;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether log exception attributes should be exported.
    /// </summary>
    public bool EmitLogExceptionAttributes { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether log event attributes should be exported.
    /// </summary>
    public bool EmitLogEventAttributes { get; set; } = false;
}