// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry;

/// <summary>
/// An interface for configuring OpenTelemetry inside an <see
/// cref="IServiceCollection"/>.
/// </summary>
public interface IOpenTelemetryBuilder
{
    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> where OpenTelemetry services
    /// are configured.
    /// </summary>
    IServiceCollection Services { get; }
}
