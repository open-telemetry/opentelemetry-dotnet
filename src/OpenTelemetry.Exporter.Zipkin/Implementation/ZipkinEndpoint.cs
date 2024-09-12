// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal sealed class ZipkinEndpoint
{
    public ZipkinEndpoint(string serviceName)
        : this(serviceName, null, null, null, null)
    {
    }

    public ZipkinEndpoint(
        string? serviceName,
        string? ipv4,
        string? ipv6,
        int? port,
        Dictionary<string, object>? tags)
    {
        this.ServiceName = serviceName;
        this.Ipv4 = ipv4;
        this.Ipv6 = ipv6;
        this.Port = port;
        this.Tags = tags;
    }

    public string? ServiceName { get; }

    public string? Ipv4 { get; }

    public string? Ipv6 { get; }

    public int? Port { get; }

    public Dictionary<string, object>? Tags { get; }

    public static ZipkinEndpoint Create(string serviceName)
    {
        return new ZipkinEndpoint(serviceName);
    }

    public static ZipkinEndpoint Create((string Name, int Port) serviceNameAndPort)
    {
        var serviceName = serviceNameAndPort.Port == default
            ? serviceNameAndPort.Name
            : $"{serviceNameAndPort.Name}:{serviceNameAndPort.Port}";

        return new ZipkinEndpoint(serviceName);
    }

    public ZipkinEndpoint Clone(string serviceName)
    {
        return new ZipkinEndpoint(
            serviceName,
            this.Ipv4,
            this.Ipv6,
            this.Port,
            this.Tags);
    }

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        if (this.ServiceName != null)
        {
            writer.WriteString(ZipkinSpanJsonHelper.ServiceNamePropertyName, this.ServiceName);
        }

        if (this.Ipv4 != null)
        {
            writer.WriteString(ZipkinSpanJsonHelper.Ipv4PropertyName, this.Ipv4);
        }

        if (this.Ipv6 != null)
        {
            writer.WriteString(ZipkinSpanJsonHelper.Ipv6PropertyName, this.Ipv6);
        }

        if (this.Port.HasValue)
        {
            writer.WriteNumber(ZipkinSpanJsonHelper.PortPropertyName, this.Port.Value);
        }

        writer.WriteEndObject();
    }
}
