// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Span execution status.
/// </summary>
public readonly struct Status : IEquatable<Status>
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    public static readonly Status Ok = new(StatusCode.Ok);

    /// <summary>
    /// The default status.
    /// </summary>
    public static readonly Status Unset = new(StatusCode.Unset);

    /// <summary>
    /// The operation contains an error.
    /// </summary>
    public static readonly Status Error = new(StatusCode.Error);

    internal Status(StatusCode statusCode, string? description = null)
    {
        this.StatusCode = statusCode;
        this.Description = description;
    }

    /// <summary>
    /// Gets the canonical code from this status.
    /// </summary>
    public StatusCode StatusCode { get; }

    /// <summary>
    /// Gets the status description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Compare two <see cref="Status"/> for equality.
    /// </summary>
    /// <param name="status1">First Status to compare.</param>
    /// <param name="status2">Second Status to compare.</param>
    public static bool operator ==(Status status1, Status status2) => status1.Equals(status2);

    /// <summary>
    /// Compare two <see cref="Status"/> for not equality.
    /// </summary>
    /// <param name="status1">First Status to compare.</param>
    /// <param name="status2">Second Status to compare.</param>
    public static bool operator !=(Status status1, Status status2) => !status1.Equals(status2);

    /// <summary>
    /// Returns a new instance of a status with the description populated.
    /// </summary>
    /// <remarks>
    /// Note: Status Description is only valid for <see
    /// cref="StatusCode.Error"/> Status and will be ignored for all other
    /// <see cref="Trace.StatusCode"/> values. See the <a
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status">Status
    /// API</a> for details.
    /// </remarks>
    /// <param name="description">Description of the status.</param>
    /// <returns>New instance of the status class with the description populated.</returns>
    public Status WithDescription(string? description)
    {
        if (this.StatusCode != StatusCode.Error || this.Description == description)
        {
            return this;
        }

        return new Status(this.StatusCode, description);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Status status && this.Equals(status);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 17;
        unchecked
        {
            hash = (31 * hash) + this.StatusCode.GetHashCode();
#if NET
            hash = (31 * hash) + (this.Description?.GetHashCode(StringComparison.Ordinal) ?? 0);
#else
            hash = (31 * hash) + (this.Description?.GetHashCode() ?? 0);
#endif
        }

        return hash;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return nameof(Status)
            + "{"
            + nameof(this.StatusCode) + "=" + this.StatusCode + ", "
            + nameof(this.Description) + "=" + this.Description
            + "}";
    }

    /// <inheritdoc/>
    public bool Equals(Status other)
        => this.StatusCode == other.StatusCode && this.Description == other.Description;
}
