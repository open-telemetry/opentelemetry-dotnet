// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources;

/// <summary>
/// An optional interface that an <see cref="IResourceDetector"/> can implement to contribute a
/// final schema URL override to a <see cref="ResourceBuilder"/>. When present, the override wins
/// over normal <see cref="Resource.Merge(Resource)"/> schema conflict behavior and is applied to
/// the final <see cref="Resource"/> produced by <see cref="ResourceBuilder.Build"/>, regardless of
/// where the detector appears in the detector list.
/// </summary>
internal interface IResourceSchemaUrlOverrideProvider
{
    /// <summary>
    /// Attempts to get the schema URL override contributed by the detector.
    /// </summary>
    /// <param name="schemaUrl">The schema URL override, if any.</param>
    /// <returns>
    /// <see langword="true"/> if the detector is contributing a schema URL override; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetSchemaUrlOverride(out string? schemaUrl);
}
