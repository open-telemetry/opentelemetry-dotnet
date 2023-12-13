// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Context;

/// <summary>
/// Describes a type of <see cref="RuntimeContextSlot{T}"/> which can expose its value as an <see cref="object"/>.
/// </summary>
public interface IRuntimeContextSlotValueAccessor
{
    /// <summary>
    /// Gets or sets the value of the slot as an <see cref="object"/>.
    /// </summary>
    object Value { get; set; }
}
