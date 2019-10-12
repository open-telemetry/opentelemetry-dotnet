// <copyright file="LightStepSpan.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.LightStep.Implementation
{
#pragma warning disable SA1402 // File may only contain a single type
    public class LightStepSpan
    {
        [JsonProperty("operationName")]
        public string OperationName { get; set; }

        [JsonProperty("startTimestamp")]
        public DateTime StartTimestamp { get; set; }

        [JsonProperty("durationMicros")]
        public ulong DurationMicros { get; set; }

        [JsonProperty("spanContext")]
        public SpanContext SpanContext { get; set; }

        [JsonProperty("tags")]
        public IList<Tag> Tags { get; set; } = new List<Tag>();

        [JsonProperty("logs")]
        public IList<Log> Logs { get; set; } = new List<Log>();

        [JsonProperty("references")]
        public IList<Reference> References { get; set; } = new List<Reference>();
    }

    public class SpanContext
    {
        [JsonProperty("traceId", NullValueHandling = NullValueHandling.Ignore)]
        public ulong TraceId { get; set; }

        [JsonProperty("spanId", NullValueHandling = NullValueHandling.Ignore)]
        public ulong SpanId { get; set; }
    }

    public class Tag
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("stringValue")]
        public string StringValue { get; set; }
    }

    public class Log
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("fields")]
        public IList<Tag> Fields { get; set; } = new List<Tag>();
    }

    public class Reference
    {
        [JsonProperty("relationship")]
        public string Relationship { get; set; }

        [JsonProperty("spanContext")]
        public SpanContext SpanContext { get; set; }
    }
}
