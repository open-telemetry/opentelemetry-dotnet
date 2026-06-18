// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The three states a declarative-configuration <c>ConfigProperty&lt;T&gt;</c> can hold.
/// </summary>
/// <remarks>
/// The OpenTelemetry configuration specification requires differentiating a <c>ConfigProperty&lt;T&gt;</c> that is
/// <see cref="Absent"/> (the key did not appear in the document) from one that is <see cref="Null"/>
/// (the key appeared with a null value). Those two states drive different behaviour at Create time
/// (the schema's <c>defaultBehavior</c> versus <c>nullBehavior</c>), so they must be tracked
/// distinctly rather than collapsed into a single "no value" state.
/// </remarks>
internal enum ConfigPropertyState
{
    /// <summary>
    /// The key did not appear in the document.
    /// </summary>
    Absent,

    /// <summary>
    /// The key appeared but selects null behavior (YAML null, empty plain scalar, or unusable value).
    /// </summary>
    Null,

    /// <summary>
    /// The key appeared with a value.
    /// </summary>
    Present,
}
