// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// The exception thrown when the tags of a metric point cannot be serialized because
/// they are larger than the largest buffer which is used to do so.
/// </summary>
/// <remarks>
/// Unlike an <see cref="IndexOutOfRangeException"/> from the response buffer, growing the response
/// buffer will not help, so only the metric which the tags belong to can be left out of the response.
/// </remarks>
#pragma warning disable CA1064 // Exceptions should be public - an internal signal between the serializer and the collection manager, never surfaced to callers.
internal sealed class SerializedTagsTooLargeException : InvalidOperationException
#pragma warning restore CA1064
{
    public SerializedTagsTooLargeException()
        : base("The serialized Prometheus tag set exceeded the maximum supported size.")
    {
    }

    public SerializedTagsTooLargeException(string message)
        : base(message)
    {
    }

    public SerializedTagsTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
