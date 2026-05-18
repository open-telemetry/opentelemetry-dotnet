// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

public class ProtobufOtlpResourceSerializerBenchmarks
{
    private static readonly KeyValuePair<string, object>[] AllAttributes =
    [
        new("service.name", "checkout-api"),
        new("service.namespace", "shop"),
        new("service.version", "2.4.1"),
        new("service.instance.id", "9a8d1f3e-4b2c-4e15-9a4f-1b2c3d4e5f6a"),
        new("telemetry.sdk.name", "opentelemetry"),
        new("telemetry.sdk.language", "dotnet"),
        new("telemetry.sdk.version", "1.13.0"),
        new("deployment.environment", "production"),
        new("host.name", "ip-10-0-12-47.eu-west-1.compute.internal"),
        new("host.id", "i-0abcdef1234567890"),
        new("host.type", "c6a.2xlarge"),
        new("host.arch", "amd64"),
        new("os.type", "linux"),
        new("os.description", "Ubuntu 22.04.4 LTS"),
        new("process.pid", 18742L),
        new("process.executable.name", "Checkout.Api"),
        new("process.runtime.name", ".NET"),
        new("process.runtime.version", "10.0.8"),
        new("container.id", "8f3a7b4c9e1d2f5a6b8c0e2f4a6b8d0c2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b"),
        new("container.image.name", "registry.example.com/shop/checkout-api"),
        new("container.image.tag", "2.4.1-abc1234"),
        new("k8s.namespace.name", "shop-prod"),
        new("k8s.pod.name", "checkout-api-7d8c9f4b6c-x9k2m"),
        new("k8s.pod.uid", "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a"),
        new("k8s.node.name", "ip-10-0-12-47.eu-west-1.compute.internal"),
        new("k8s.deployment.name", "checkout-api"),
        new("k8s.cluster.name", "shop-prod-eu-west-1"),
        new("cloud.provider", "aws"),
        new("cloud.region", "eu-west-1"),
        new("cloud.availability_zone", "eu-west-1a"),
        new("cloud.account.id", "123456789012"),
        new("deployment.id", "deploy-2024-05-17-abc"),
    ];

    private readonly byte[] buffer = new byte[64 * 1024];
    private Resource resource = Resource.Empty;

    [Params(0, 4, 8, 16, 32)]
    public int AttributeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (this.AttributeCount == 0)
        {
            this.resource = Resource.Empty;
            return;
        }

        var attributes = new Dictionary<string, object>(this.AttributeCount);
        var count = Math.Min(this.AttributeCount, AllAttributes.Length);
        for (var i = 0; i < count; i++)
        {
            attributes[AllAttributes[i].Key] = AllAttributes[i].Value;
        }

        for (var i = count; i < this.AttributeCount; i++)
        {
            attributes[$"custom.attribute.{i}"] = Guid.NewGuid().ToString();
        }

        this.resource = new Resource(attributes);
    }

    [Benchmark]
    public int WriteResource() => ProtobufOtlpResourceSerializer.WriteResource(this.buffer, 0, this.resource);
}
