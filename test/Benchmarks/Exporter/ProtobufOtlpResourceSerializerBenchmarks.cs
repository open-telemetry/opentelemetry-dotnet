// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

public class ProtobufOtlpResourceSerializerBenchmarks
{
    private readonly byte[] buffer = new byte[32 * 1024];
    private Resource resource = Resource.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Test only.")]
    public enum ResourceShape
    {
        Empty,
        Default,
        Service,
        Production,
    }

    [Params(ResourceShape.Empty, ResourceShape.Default, ResourceShape.Service, ResourceShape.Production)]
    public ResourceShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.resource = this.Shape switch
        {
            ResourceShape.Empty => Resource.Empty,
            ResourceShape.Default => BuildDefault(),
            ResourceShape.Service => BuildService(),
            ResourceShape.Production => BuildProduction(),
            _ => Resource.Empty,
        };
    }

    [Benchmark]
    public int WriteResource() => ProtobufOtlpResourceSerializer.WriteResource(this.buffer, 0, this.resource);

    private static Resource BuildDefault()
        => new(new Dictionary<string, object>
        {
            ["service.name"] = "unknown_service:benchmark",
            ["telemetry.sdk.name"] = "opentelemetry",
            ["telemetry.sdk.language"] = "dotnet",
            ["telemetry.sdk.version"] = "1.13.0",
        });

    private static Resource BuildService()
        => new(new Dictionary<string, object>
        {
            ["service.name"] = "checkout-api",
            ["service.namespace"] = "shop",
            ["service.version"] = "2.4.1",
            ["service.instance.id"] = "9a8d1f3e-4b2c-4e15-9a4f-1b2c3d4e5f6a",
            ["telemetry.sdk.name"] = "opentelemetry",
            ["telemetry.sdk.language"] = "dotnet",
            ["telemetry.sdk.version"] = "1.13.0",
            ["deployment.environment"] = "production",
            ["host.name"] = "ip-10-0-12-47.eu-west-1.compute.internal",
            ["host.id"] = "i-0abcdef1234567890",
        });

    private static Resource BuildProduction()
        => new(new Dictionary<string, object>
        {
            ["service.name"] = "checkout-api",
            ["service.namespace"] = "shop",
            ["service.version"] = "2.4.1",
            ["service.instance.id"] = "9a8d1f3e-4b2c-4e15-9a4f-1b2c3d4e5f6a",
            ["telemetry.sdk.name"] = "opentelemetry",
            ["telemetry.sdk.language"] = "dotnet",
            ["telemetry.sdk.version"] = "1.13.0",
            ["deployment.environment"] = "production",
            ["host.name"] = "ip-10-0-12-47.eu-west-1.compute.internal",
            ["host.id"] = "i-0abcdef1234567890",
            ["host.type"] = "c6a.2xlarge",
            ["host.arch"] = "amd64",
            ["os.type"] = "linux",
            ["os.description"] = "Ubuntu 22.04.4 LTS",
            ["process.pid"] = 18742L,
            ["process.executable.name"] = "Checkout.Api",
            ["process.runtime.name"] = ".NET",
            ["process.runtime.version"] = "10.0.8",
            ["container.id"] = "8f3a7b4c9e1d2f5a6b8c0e2f4a6b8d0c2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b",
            ["container.image.name"] = "registry.example.com/shop/checkout-api",
            ["container.image.tag"] = "2.4.1-abc1234",
            ["k8s.namespace.name"] = "shop-prod",
            ["k8s.pod.name"] = "checkout-api-7d8c9f4b6c-x9k2m",
            ["k8s.pod.uid"] = "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
            ["k8s.node.name"] = "ip-10-0-12-47.eu-west-1.compute.internal",
            ["k8s.deployment.name"] = "checkout-api",
            ["k8s.cluster.name"] = "shop-prod-eu-west-1",
            ["cloud.provider"] = "aws",
            ["cloud.region"] = "eu-west-1",
            ["cloud.availability_zone"] = "eu-west-1a",
            ["cloud.account.id"] = "123456789012",
        });
}
