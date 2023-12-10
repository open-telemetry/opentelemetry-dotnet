// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources;

/// <summary>
/// An interface for Resource detectors.
/// </summary>
public interface IResourceDetector
{
    /// <summary>
    /// Called to get a resource with attributes from detector.
    /// </summary>
    /// <returns>An instance of <see cref="Resource"/>.</returns>
    Resource Detect();
}
