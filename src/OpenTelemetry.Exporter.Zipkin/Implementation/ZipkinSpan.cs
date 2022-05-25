// <copyright file="ZipkinSpan.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal readonly struct ZipkinSpan
    {
        public ZipkinSpan(
            string traceId,
            string parentId,
            string id,
            string kind,
            string name,
            long? timestamp,
            long? duration,
            ZipkinEndpoint localEndpoint,
            ZipkinEndpoint remoteEndpoint,
            in PooledList<ZipkinAnnotation> annotations,
            in PooledList<KeyValuePair<string, object>> tags,
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

        public string ParentId { get; }

        public string Id { get; }

        public string Kind { get; }

        public string Name { get; }

        public long? Timestamp { get; }

        public long? Duration { get; }

        public ZipkinEndpoint LocalEndpoint { get; }

        public ZipkinEndpoint RemoteEndpoint { get; }

        public PooledList<ZipkinAnnotation> Annotations { get; }

        public PooledList<KeyValuePair<string, object>> Tags { get; }

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

            if (!this.Tags.IsEmpty || this.LocalEndpoint.Tags != null)
            {
                writer.WritePropertyName(ZipkinSpanJsonHelper.TagsPropertyName);
                writer.WriteStartObject();

                // this will be used when we convert int, double, int[], double[] to string
                var originalUICulture = Thread.CurrentThread.CurrentUICulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                try
                {
                    foreach (var tag in this.LocalEndpoint.Tags ?? Enumerable.Empty<KeyValuePair<string, object>>())
                    {
                        if (ZipkinTagTransformer.Instance.TryTransformTag(tag, out var result))
                        {
                            writer.WriteString(tag.Key, result);
                        }
                    }

                    foreach (var tag in this.Tags)
                    {
                        if (ZipkinTagTransformer.Instance.TryTransformTag(tag, out var result))
                        {
                            writer.WriteString(tag.Key, result);
                        }
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
}
