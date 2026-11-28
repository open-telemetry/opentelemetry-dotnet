// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Options for Process Context Sharing.
/// </summary>
/// <remarks>
/// <para experimental-warning="true"><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</para>
/// </remarks>
[Experimental(DiagnosticDefinitions.ProcessContextExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
/// <summary>
/// Options for Process Context Sharing.
/// </summary>
internal
#endif
    sealed class ProcessContextOptions
{
    internal const string EnabledEnvVarKey = "OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessContextOptions"/> class.
    /// </summary>
    public ProcessContextOptions()
        : this(EnvironmentVariablesConfiguration.Create())
    {
    }

    internal ProcessContextOptions(IConfiguration configuration)
    {
        if (configuration.TryGetBoolValue(ProcessContextEventSource.Log, EnabledEnvVarKey, out var enabled))
        {
            this.Enabled = enabled;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the process context is
    /// published. Defaults to the value of the
    /// <c>OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED</c> configuration key
    /// (environment variable), or <see langword="false"/> when it is not set.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Resources.Resource"/> to publish instead of
    /// the provider's resolved resource. Defaults to <see langword="null"/>,
    /// meaning the provider's resource is published.
    /// </summary>
    public Resource? Resource { get; set; }
}
