// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A resolved YAML string key, its value node, and the key's occurrence position.
/// </summary>
internal readonly record struct ResolvedYamlMappingEntry(string Key, YamlNode Value, ConfigValuePosition KeyPosition);
