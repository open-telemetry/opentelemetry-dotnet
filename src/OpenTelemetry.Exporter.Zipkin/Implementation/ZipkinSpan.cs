// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal readonly struct ZipkinSpan
{
    public ZipkinSpan(
        string traceId,
        string? parentId,
        string id,
        string? kind,
        string name,
        long? timestamp,
        long? duration,
        ZipkinEndpoint localEndpoint,
        ZipkinEndpoint? remoteEndpoint,
        in PooledList<ZipkinAnnotation> annotations,
        in PooledList<KeyValuePair<string, object?>> tags,
        bool? debug,
        bool? shared)
    {
        Guard.ThrowIfNullOrWhitespace(traceId);
        Guard.ThrowIfNullOrWhitespace(id);

        this.TraceId = traceId;
        this.ParentId = parentId;
        this.Id = id;
        this.Kind = kind;
        this.Name = name;
        this.Timestamp = timestamp;
        this.Duration = duration;
        this.LocalEndpoint = localEndpoint;
        this.RemoteEndpoint = remoteEndpoint;
        this.Annotations = annotations;
        this.Tags = tags;
        this.Debug = debug;
        this.Shared = shared;
    }

    public string TraceId { get; }

    public string? ParentId { get; }

    public string Id { get; }

    public string? Kind { get; }

    public string Name { get; }

    public long? Timestamp { get; }

    public long? Duration { get; }

    public ZipkinEndpoint LocalEndpoint { get; }

    public ZipkinEndpoint? RemoteEndpoint { get; }

    public PooledList<ZipkinAnnotation> Annotations { get; }

    public PooledList<KeyValuePair<string, object?>> Tags { get; }

    public bool? Debug { get; }

    public bool? Shared { get; }

    public void Return()
    {
        this.Annotations.Return();
        this.Tags.Return();
    }

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        writer.WriteString(ZipkinSpanJsonHelper.TraceIdPropertyName, this.TraceId);

        if (this.Name != null)
        {
            writer.WriteString(ZipkinSpanJsonHelper.NamePropertyName, this.Name);
        }

        if (this.ParentId != null)
        {
            writer.WriteString(ZipkinSpanJsonHelper.ParentIdPropertyName, this.ParentId);
        }

        writer.WriteString(ZipkinSpanJsonHelper.IdPropertyName, this.Id);

        if (this.Kind != null)
        {
            writer.WriteString(ZipkinSpanJsonHelper.KindPropertyName, this.Kind);
        }

        if (this.Timestamp.HasValue)
        {
            writer.WriteNumber(ZipkinSpanJsonHelper.TimestampPropertyName, this.Timestamp.Value);
        }

        if (this.Duration.HasValue)
        {
            writer.WriteNumber(ZipkinSpanJsonHelper.DurationPropertyName, this.Duration.Value);
        }

        if (this.Debug.HasValue)
        {
            writer.WriteBoolean(ZipkinSpanJsonHelper.DebugPropertyName, this.Debug.Value);
        }

        if (this.Shared.HasValue)
        {
            writer.WriteBoolean(ZipkinSpanJsonHelper.SharedPropertyName, this.Shared.Value);
        }

        if (this.LocalEndpoint != null)
        {
            writer.WritePropertyName(ZipkinSpanJsonHelper.LocalEndpointPropertyName);
            this.LocalEndpoint.Write(writer);
        }

        if (this.RemoteEndpoint != null)
        {
            writer.WritePropertyName(ZipkinSpanJsonHelper.RemoteEndpointPropertyName);
            this.RemoteEndpoint.Write(writer);
        }

        if (!this.Annotations.IsEmpty)
        {
            writer.WritePropertyName(ZipkinSpanJsonHelper.AnnotationsPropertyName);
            writer.WriteStartArray();

            foreach (var annotation in this.Annotations)
            {
                writer.WriteStartObject();

                writer.WriteNumber(ZipkinSpanJsonHelper.TimestampPropertyName, annotation.Timestamp);

                writer.WriteString(ZipkinSpanJsonHelper.ValuePropertyName, annotation.Value);

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (!this.Tags.IsEmpty || this.LocalEndpoint!.Tags != null)
        {
            writer.WritePropertyName(ZipkinSpanJsonHelper.TagsPropertyName);
            writer.WriteStartObject();

            // Note: The spec says "Primitive types MUST be converted to string using en-US culture settings"
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#attribute

            var originalUICulture = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            try
            {
                foreach (var tag in this.LocalEndpoint!.Tags! ?? Enumerable.Empty<KeyValuePair<string, object?>>())
                {
                    ZipkinTagWriter.Instance.TryWriteTag(ref writer, tag);
                }

                foreach (var tag in this.Tags)
                {
                    ZipkinTagWriter.Instance.TryWriteTag(ref writer, tag);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
