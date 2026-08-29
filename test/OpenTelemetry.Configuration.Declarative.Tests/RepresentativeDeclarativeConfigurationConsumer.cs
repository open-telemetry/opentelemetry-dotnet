// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

internal sealed class RepresentativeDeclarativeConfigurationConsumer(
    DeclarativeConfigurationDocumentAccessor? accessor)
{
    internal DeclarativeConfigurationDocumentAccessor? Accessor { get; } = accessor;

    internal DeclarativeConfigurationDocument? Document => this.Accessor?.GetDocument();
}
