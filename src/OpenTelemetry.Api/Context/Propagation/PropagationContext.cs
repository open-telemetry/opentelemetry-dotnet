// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// Stores propagation data.
/// </summary>
public readonly struct PropagationContext : IEquatable<PropagationContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropagationContext"/> struct.
    /// </summary>
    /// <param name="activityContext"><see cref="System.Diagnostics.ActivityContext"/>.</param>
    /// <param name="baggage"><see cref="Baggage"/>.</param>
    public PropagationContext(ActivityContext activityContext, Baggage baggage)
    {
        this.ActivityContext = activityContext;
        this.Baggage = baggage;
    }

    /// <summary>
    /// Gets <see cref="System.Diagnostics.ActivityContext"/>.
    /// </summary>
    public ActivityContext ActivityContext { get; }

    /// <summary>
    /// Gets <see cref="Baggage"/>.
    /// </summary>
    public Baggage Baggage { get; }

    /// <summary>
    /// Compare two entries of <see cref="PropagationContext"/> for equality.
    /// </summary>
    /// <param name="left">First Entry to compare.</param>
    /// <param name="right">Second Entry to compare.</param>
    public static bool operator ==(PropagationContext left, PropagationContext right) => left.Equals(right);

    /// <summary>
    /// Compare two entries of <see cref="PropagationContext"/> for not equality.
    /// </summary>
    /// <param name="left">First Entry to compare.</param>
    /// <param name="right">Second Entry to compare.</param>
    public static bool operator !=(PropagationContext left, PropagationContext right) => !(left == right);

    /// <inheritdoc/>
    public bool Equals(PropagationContext value)
    {
        return this.ActivityContext == value.ActivityContext
            && this.Baggage == value.Baggage;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => (obj is PropagationContext context) && this.Equals(context);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 323591981;
        unchecked
        {
            hash = (hash * -1521134295) + this.ActivityContext.GetHashCode();
            hash = (hash * -1521134295) + this.Baggage.GetHashCode();
        }

        return hash;
    }
}
