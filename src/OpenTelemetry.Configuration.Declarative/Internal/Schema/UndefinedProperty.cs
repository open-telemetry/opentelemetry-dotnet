// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A property the schema does not define that is retained in the document.
/// </summary>
/// <param name="Path">The dotted path of the property from the document root.</param>
/// <param name="Kind">Why the property was retained.</param>
internal readonly record struct UndefinedProperty(string Path, UndefinedPropertyKind Kind);
