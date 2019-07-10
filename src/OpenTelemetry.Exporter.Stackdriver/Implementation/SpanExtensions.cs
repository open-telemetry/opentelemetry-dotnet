// <copyright file="SpanExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    using System.Linq;
    using Google.Cloud.Trace.V2;
    using OpenTelemetry.Exporter.Stackdriver.Utils;
    using OpenTelemetry.Trace;

    internal static class SpanExtensions
    {
        /// <summary>
        /// Translating <see cref="SpanData"/> to Stackdriver's Span
        /// According to <see href="https://cloud.google.com/trace/docs/reference/v2/rpc/google.devtools.cloudtrace.v2"/> specifications.
        /// </summary>
        /// <param name="spanData">Span in OpenTelemetry format.</param>
        /// <param name="projectId">Google Cloud Platform Project Id.</param>
        /// <returns><see cref="ISpan"/>.</returns>
        public static Google.Cloud.Trace.V2.Span ToSpan(this SpanData spanData, string projectId)
        {
            var spanId = spanData.Context.SpanId.ToLowerBase16();

            // Base span settings
            var span = new Google.Cloud.Trace.V2.Span
            {
                SpanName = new SpanName(projectId, spanData.Context.TraceId.ToLowerBase16(), spanId),
                SpanId = spanId,
                DisplayName = new TruncatableString { Value = spanData.Name },
                StartTime = spanData.StartTimestamp.ToTimestamp(),
                EndTime = spanData.EndTimestamp.ToTimestamp(),
                ChildSpanCount = spanData.ChildSpanCount,
            };
            if (spanData.ParentSpanId != null)
            {
                var parentSpanId = spanData.ParentSpanId.ToLowerBase16();
                if (!string.IsNullOrEmpty(parentSpanId))
                {
                    span.ParentSpanId = parentSpanId;
                }
            }

            // Span Links
            if (spanData.Links != null)
            {
                span.Links = new Google.Cloud.Trace.V2.Span.Types.Links
                {
                    DroppedLinksCount = spanData.Links.DroppedLinksCount,
                    Link = { spanData.Links.Links.Select(l => l.ToLink()) },
                };
            }

            // Span Attributes
            if (spanData.Attributes != null)
            {
                span.Attributes = new Google.Cloud.Trace.V2.Span.Types.Attributes
                {
                    DroppedAttributesCount = spanData.Attributes != null ? spanData.Attributes.DroppedAttributesCount : 0,

                    AttributeMap =
                    {
                        spanData.Attributes?.AttributeMap?.ToDictionary(
                                        s => s.Key,
                                        s => s.Value?.ToAttributeValue()),
                    },
                };
            }

            return span;
        }

        public static Google.Cloud.Trace.V2.Span.Types.Link ToLink(this ILink link)
        {
            var ret = new Google.Cloud.Trace.V2.Span.Types.Link();
            ret.SpanId = link.Context.SpanId.ToLowerBase16();
            ret.TraceId = link.Context.TraceId.ToLowerBase16();

            if (link.Attributes != null)
            {
                ret.Attributes = new Google.Cloud.Trace.V2.Span.Types.Attributes
                {
                    DroppedAttributesCount = OpenTelemetry.Trace.Config.TraceParams.Default.MaxNumberOfAttributes - link.Attributes.Count,

                    AttributeMap =
                    {
                        link.Attributes.ToDictionary(
                         att => att.Key,
                         att => att.Value.ToAttributeValue()),
                    },
                };
            }

            return ret;
        }

        public static Google.Cloud.Trace.V2.AttributeValue ToAttributeValue(this IAttributeValue av)
        {
            var ret = av.Match(
                (s) => new Google.Cloud.Trace.V2.AttributeValue() { StringValue = new TruncatableString() { Value = s } },
                (b) => new Google.Cloud.Trace.V2.AttributeValue() { BoolValue = b },
                (l) => new Google.Cloud.Trace.V2.AttributeValue() { IntValue = l },
                (d) => new Google.Cloud.Trace.V2.AttributeValue() { StringValue = new TruncatableString() { Value = d.ToString() } },
                (obj) => new Google.Cloud.Trace.V2.AttributeValue() { StringValue = new TruncatableString() { Value = obj.ToString() } });

            return ret;
        }
    }
}
