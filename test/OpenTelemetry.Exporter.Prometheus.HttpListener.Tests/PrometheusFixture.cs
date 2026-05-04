// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusFixture : XunitContainerFixture<IContainer>
{
    protected override string DockerfileName => "prometheus.Dockerfile";

    protected override IContainer CreateContainer() =>
        new ContainerBuilder(this.GetImage())
            .WithPortBinding(9090)
            .Build();
}
