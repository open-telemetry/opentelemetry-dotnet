// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The parsed result of a declarative configuration document: the typed model, its
/// flat <c>OTEL_*</c> key projection, and a schemaless view of the whole document, produced
/// together from a single file read.
/// </summary>
/// <param name="Model">
/// The typed configuration model. Deeply immutable: collection-valued nodes cannot be modified
/// through the returned instance.
/// </param>
/// <param name="FlatKeys">
/// The flat <c>OTEL_*</c> key projection derived from <paramref name="Model"/>.
/// Immutable from first publication.
/// </param>
/// <param name="Properties">
/// Every key the document contained.
/// </param>
internal sealed record DeclarativeConfigurationDocument(
    DeclarativeConfiguration Model,
    ReadOnlyDictionary<string, string?> FlatKeys,
    ConfigProperties Properties);
