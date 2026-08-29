// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A scalar value after environment substitution and YAML 1.2 core-schema resolution.
/// </summary>
internal readonly record struct ResolvedYamlScalar(string Value, YamlScalarKind Kind);
