// <copyright file="ZipkinSpan.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Exporter.Zipkin.Implementation
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal class ZipkinSpan
    {
        [JsonProperty("traceId")]
        public string TraceId { get; set; }

        [JsonProperty("parentId")]
        public string ParentId { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kind")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ZipkinSpanKind Kind { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("localEndpoint")]
        public ZipkinEndpoint LocalEndpoint { get; set; }

        [JsonProperty("remoteEndpoint")]
        public ZipkinEndpoint RemoteEndpoint { get; set; }

        [JsonProperty("annotations")]
        public List<ZipkinAnnotation> Annotations { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string, string> Tags { get; set; }

        [JsonProperty("debug")]
        public bool Debug { get; set; }

        [JsonProperty("shared")]
        public bool Shared { get; set; }

        public static Builder NewBuilder()
        {
            return new Builder();
        }

        public class Builder
        {
            private readonly ZipkinSpan result = new ZipkinSpan();

            internal Builder TraceId(string val)
            {
                this.result.TraceId = val;
                return this;
            }

            internal Builder Id(string val)
            {
                this.result.Id = val;
                return this;
            }

            internal Builder ParentId(string val)
            {
                this.result.ParentId = val;
                return this;
            }

            internal Builder Kind(ZipkinSpanKind val)
            {
                this.result.Kind = val;
                return this;
            }

            internal Builder Name(string val)
            {
                this.result.Name = val;
                return this;
            }

            internal Builder Timestamp(long val)
            {
                this.result.Timestamp = val;
                return this;
            }

            internal Builder Duration(long val)
            {
                this.result.Duration = val;
                return this;
            }

            internal Builder LocalEndpoint(ZipkinEndpoint val)
            {
                this.result.LocalEndpoint = val;
                return this;
            }

            internal Builder RemoteEndpoint(ZipkinEndpoint val)
            {
                this.result.RemoteEndpoint = val;
                return this;
            }

            internal Builder Debug(bool val)
            {
                this.result.Debug = val;
                return this;
            }

            internal Builder Shared(bool val)
            {
                this.result.Shared = val;
                return this;
            }

            internal Builder PutTag(string key, string value)
            {
                if (this.result.Tags == null)
                {
                    this.result.Tags = new Dictionary<string, string>();
                }

                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                this.result.Tags[key] = value ?? throw new ArgumentNullException(nameof(value));

                return this;
            }

            internal Builder AddAnnotation(long timestamp, string value)
            {
                if (this.result.Annotations == null)
                {
                    this.result.Annotations = new List<ZipkinAnnotation>(2);
                }

                this.result.Annotations.Add(new ZipkinAnnotation() { Timestamp = timestamp, Value = value });

                return this;
            }

            internal ZipkinSpan Build()
            {
                if (this.result.TraceId == null)
                {
                    throw new ArgumentException("Trace ID should not be null");
                }

                if (this.result.Id == null)
                {
                    throw new ArgumentException("ID should not be null");
                }

                return this.result;
            }
        }
    }
}
