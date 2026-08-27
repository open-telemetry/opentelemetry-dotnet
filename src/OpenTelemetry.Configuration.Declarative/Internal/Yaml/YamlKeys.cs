// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Declarative-configuration YAML key names (snake_case, as they appear in the document).
/// </summary>
internal static class YamlKeys
{
    internal const string FileFormat = "file_format";
    internal const string Disabled = "disabled";
    internal const string Resource = "resource";
    internal const string Attributes = "attributes";
    internal const string AttributesList = "attributes_list";
    internal const string Name = "name";
    internal const string Value = "value";
    internal const string Type = "type";
}
