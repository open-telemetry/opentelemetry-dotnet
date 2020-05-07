﻿// <copyright file="ZipkinSpan.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using System.Text.Json;
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
            in PooledList<ZipkinAnnotation>? annotations,
            in PooledList<KeyValuePair<string, string>>? tags,
            bool? debug,
            bool? shared)
        {
            if (string.IsNullOrWhiteSpace(traceId))
            {
                throw new ArgumentNullException(nameof(traceId));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

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

        public PooledList<ZipkinAnnotation>? Annotations { get; }

        public PooledList<KeyValuePair<string, string>>? Tags { get; }

        public bool? Debug { get; }

        public bool? Shared { get; }

        public void Return()
        {
            this.Annotations?.Return();
            this.Tags?.Return();
        }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WriteString("traceId", this.TraceId);

            if (this.Name != null)
            {
                writer.WriteString("name", this.Name);
            }

            if (this.ParentId != null)
            {
                writer.WriteString("parentId", this.ParentId);
            }

            writer.WriteString("id", this.Id);

            writer.WriteString("kind", this.Kind);

            if (this.Timestamp.HasValue)
            {
                writer.WriteNumber("timestamp", this.Timestamp.Value);
            }

            if (this.Duration.HasValue)
            {
                writer.WriteNumber("duration", this.Duration.Value);
            }

            if (this.Debug.HasValue)
            {
                writer.WriteBoolean("debug", this.Debug.Value);
            }

            if (this.Shared.HasValue)
            {
                writer.WriteBoolean("shared", this.Shared.Value);
            }

            if (this.LocalEndpoint != null)
            {
                writer.WritePropertyName("localEndpoint");
                this.LocalEndpoint.Write(writer);
            }

            if (this.RemoteEndpoint != null)
            {
                writer.WritePropertyName("remoteEndpoint");
                this.RemoteEndpoint.Write(writer);
            }

            if (this.Annotations.HasValue)
            {
                writer.WritePropertyName("annotations");
                writer.WriteStartArray();

                foreach (var annotation in this.Annotations.Value)
                {
                    writer.WriteStartObject();

                    writer.WriteNumber("timestamp", annotation.Timestamp);

                    writer.WriteString("value", annotation.Value);

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (this.Tags.HasValue)
            {
                writer.WritePropertyName("tags");
                writer.WriteStartObject();

                foreach (var tag in this.Tags.Value)
                {
                    writer.WriteString(tag.Key, tag.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }
}
