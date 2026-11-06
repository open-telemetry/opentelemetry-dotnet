// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A schema-defined property that is present in the document but not applied by this package.
/// </summary>
/// <param name="Path">The dotted path of the property from the document root.</param>
/// <param name="Stability">The stability of the property.</param>
/// <param name="Position">The position at which the property was authored.</param>
internal readonly record struct UnappliedProperty(string Path, SchemaStability Stability, ConfigValuePosition Position);
