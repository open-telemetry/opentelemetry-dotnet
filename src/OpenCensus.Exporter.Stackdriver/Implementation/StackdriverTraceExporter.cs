// <copyright file="ApplicationInsightsExporter.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Exporter.Stackdriver.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Google.Api.Gax.Grpc;
    using Google.Cloud.Trace.V2;
    using Grpc.Core;
    using OpenCensus.Exporter.Stackdriver.Utils;
    using OpenCensus.Trace;
    using OpenCensus.Trace.Export;

    static class SpanExtensions
    {
        /// <summary>
        /// Translating <see cref="ISpanData"/> to Stackdriver's Span
        /// According to <see href="https://cloud.google.com/trace/docs/reference/v2/rpc/google.devtools.cloudtrace.v2"/> specifications
        /// </summary>
        /// <param name="spanData">Span in OpenCensus format</param>
        /// <param name="projectId">Google Cloud Platform Project Id</param>
        /// <returns></returns>
        public static Span ToSpan(this ISpanData spanData, string projectId)
        {
            string spanId = spanData.Context.SpanId.ToLowerBase16();

            // Base span settings
            var span = new Span
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
                string parentSpanId = spanData.ParentSpanId.ToLowerBase16();
                if (!string.IsNullOrEmpty(parentSpanId))
                {
                    span.ParentSpanId = parentSpanId;
                }
            }

            // Span Links
            if (spanData.Links != null)
            {
                span.Links = new Span.Types.Links
                {
                    DroppedLinksCount = spanData.Links.DroppedLinksCount,
                    Link = { spanData.Links.Links.Select(l => l.ToLink()) }
                };
            }

            // Span Attributes
            if (spanData.Attributes != null)
            {
                span.Attributes = new Span.Types.Attributes
                {
                    DroppedAttributesCount = spanData.Attributes != null ? spanData.Attributes.DroppedAttributesCount : 0,

                    AttributeMap = { spanData.Attributes?.AttributeMap?.ToDictionary(
                                        s => s.Key,
                                        s => s.Value?.ToAttributeValue()) },
                };
            }

            return span;
        }

        public static Span.Types.Link ToLink(this ILink link)
        {
            var ret = new Span.Types.Link();
            ret.SpanId = link.SpanId.ToLowerBase16();
            ret.TraceId = link.TraceId.ToLowerBase16();

            if (link.Attributes != null)
            {
                ret.Attributes = new Span.Types.Attributes
                {

                    DroppedAttributesCount = OpenCensus.Trace.Config.TraceParams.Default.MaxNumberOfAttributes - link.Attributes.Count,

                    AttributeMap = { link.Attributes.ToDictionary(
                         att => att.Key,
                         att => att.Value.ToAttributeValue()) }
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

    /// <summary>
    /// Exports a group of spans to Stackdriver
    /// </summary>
    internal class StackdriverTraceExporter : IHandler
    {
        private static string STACKDRIVER_EXPORTER_VERSION;
        private static string OPENCENSUS_EXPORTER_VERSION;

        private readonly Google.Api.Gax.ResourceNames.ProjectName googleCloudProjectId;
        private readonly TraceServiceSettings traceServiceSettings;

        public StackdriverTraceExporter(string projectId)
        {
            googleCloudProjectId = new Google.Api.Gax.ResourceNames.ProjectName(projectId);

            // Set header mutation for every outgoing API call to Stackdriver so the BE knows
            // which version of OC client is calling it as well as which version of the exporter
            CallSettings callSettings = CallSettings.FromHeaderMutation(StackdriverCallHeaderAppender);
            traceServiceSettings = new TraceServiceSettings();
            traceServiceSettings.CallSettings = callSettings;
        }

        static StackdriverTraceExporter()
        {
            try
            {
                string assemblyPackageVersion = typeof(StackdriverTraceExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
                STACKDRIVER_EXPORTER_VERSION = assemblyPackageVersion;
            }
            catch (Exception)
            {
                STACKDRIVER_EXPORTER_VERSION = $"{Constants.PACKAGE_VERSION_UNDEFINED}";
            }

            try
            {
                OPENCENSUS_EXPORTER_VERSION = Assembly.GetCallingAssembly().GetName().Version.ToString();
            }
            catch (Exception)
            {
                OPENCENSUS_EXPORTER_VERSION = $"{Constants.PACKAGE_VERSION_UNDEFINED}";
            }
        }

        public async Task ExportAsync(IEnumerable<ISpanData> spanDataList)
        {
            TraceServiceClient traceWriter = TraceServiceClient.Create(settings: traceServiceSettings);
            
            var batchSpansRequest = new BatchWriteSpansRequest
            {
                ProjectName = googleCloudProjectId,
                Spans = { spanDataList.Select(s => s.ToSpan(googleCloudProjectId.ProjectId)) },
            };
            
            await traceWriter.BatchWriteSpansAsync(batchSpansRequest);
        }

        /// <summary>
        /// Appends OpenCensus headers for every outgoing request to Stackdriver Backend
        /// </summary>
        /// <param name="metadata">The metadata that is sent with every outgoing http request</param>
        private static void StackdriverCallHeaderAppender(Metadata metadata)
        {
            
            metadata.Add("AGENT_LABEL_KEY", "g.co/agent");
            metadata.Add("AGENT_LABEL_VALUE_STRING", $"{OPENCENSUS_EXPORTER_VERSION}; stackdriver-exporter {STACKDRIVER_EXPORTER_VERSION}");
        }
    }
}