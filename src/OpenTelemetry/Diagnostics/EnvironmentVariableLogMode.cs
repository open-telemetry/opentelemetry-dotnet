// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Controls how much of the environment variable snapshot is written to the
/// self-diagnostics log file preamble.
/// </summary>
/// <remarks>
/// <para>
/// Variable <i>values</i> are the sensitive part. Under the default
/// <see cref="KnownSafeValues"/> a value is shown only when the SDK recognises the variable as
/// generally safe to disclose; everything else is redacted. A variable that nobody has classified
/// therefore loses information rather than leaking a credential.
/// </para>
/// </remarks>
public enum EnvironmentVariableLogMode
{
    /// <summary>
    /// The environment variable section is omitted entirely.
    /// </summary>
    None = 0,

    /// <summary>
    /// Names only for all <c>OTEL_*</c> variables that are set are listed. No values are shown.
    /// </summary>
    Names = 1,

    /// <summary>
    /// Names of all <c>OTEL_*</c> variables that are set are listed. Values are shown only for
    /// variables the SDK recognises as safe to disclose; all other values are redacted.
    /// Endpoint values are reduced to their authority and <c>OTEL_RESOURCE_ATTRIBUTES</c> is
    /// redacted per key. This is the default.
    /// </summary>
    KnownSafeValues = 2,

    /// <summary>
    /// Names and values of all <c>OTEL_*</c> variables that are set are shown verbatim,
    /// potentially including credentials. Enable this only when deliberately capturing a full configuration
    /// snapshot, and treat the resulting file as a sensitive document.
    /// </summary>
    AllValues = 3,
}
