// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An integer property read from a document, with its provenance.
/// </summary>
/// <param name="Value">The value the user supplied, or the default when none was supplied.</param>
/// <param name="Position">The position of the value in the document, which may be unknown.</param>
/// <param name="IsSpecified">Whether the user supplied the value rather than it taking the default.</param>
internal readonly record struct IntegerSetting(int Value, ConfigValuePosition Position, bool IsSpecified);
