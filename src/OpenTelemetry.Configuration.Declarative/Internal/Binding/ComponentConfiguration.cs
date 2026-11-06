// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A component selected by name, such as a span processor, exporter or ID generator.
/// </summary>
/// <remarks>
/// The schema models a pluggable component as a mapping with exactly one key, where the key
/// selects the component and its value configures it. This record is the bound form of that node.
/// </remarks>
/// <param name="Name">The component name, the single key of its configuration node.</param>
/// <param name="Properties">The component's configuration, or empty when its value is null.</param>
/// <param name="Path">The document path of the component's key.</param>
/// <param name="Position">The position of the component's key.</param>
internal sealed record ComponentConfiguration(
    string Name,
    ConfigProperties Properties,
    string Path,
    ConfigValuePosition Position);
