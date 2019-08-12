// <copyright file="TraceExporterHandler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.LightStep.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    public class TraceExporterHandler : IHandler
    {
        private readonly LightStepTraceExporterOptions options;
        private readonly HttpClient httpClient;

        public TraceExporterHandler(LightStepTraceExporterOptions options, HttpClient client)
        {
            this.options = options;
            this.httpClient = client ?? new HttpClient();
            this.httpClient.Timeout = this.options.SatelliteTimeout;
        }

        public async Task ExportAsync(IEnumerable<SpanData> spanDataList)
        {
            var lsReport = new LightStepReport
            {
                Auth = new Authentication { AccessToken = this.options.AccessToken },
                Reporter = new Reporter
                {
                    // TODO: ReporterID should be randomly generated.
                    ReporterId = 219,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "lightstep.component_name", StringValue = this.options.ServiceName },
                    },
                },
            };

            foreach (var data in spanDataList)
            {
                lsReport.Spans.Add(this.GenerateSpan(data));
            }

            try
            {
                await this.SendSpansAsync(lsReport);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private LightStepSpan GenerateSpan(SpanData data)
        {
            var duration = data.EndTimestamp - data.StartTimestamp;
            var span = new LightStepSpan();

            if (data.ParentSpanId != default)
            {
                var sc = new SpanContext { SpanId = Convert.ToUInt64(data.ParentSpanId.ToHexString(), 16) };
                span.References.Add(new Reference
                {
                    Relationship = "CHILD_OF", SpanContext = sc,
                });
            }

            span.OperationName = data.Name;
            var sid = Convert.ToUInt64(data.Context.SpanId.ToHexString(), 16);

            var longTraceId = data.Context.TraceId.ToHexString()
                .Substring(data.Context.TraceId.ToHexString().Length - 16, 16);
            var tid = Convert.ToUInt64(longTraceId, 16);
            span.SpanContext = new SpanContext
            {
                SpanId = sid, TraceId = tid,
            };
            span.StartTimestamp = data.StartTimestamp;
            span.DurationMicros = Convert.ToUInt64(Math.Abs(duration.Ticks) / 10);

            foreach (var attr in data.Attributes.AttributeMap)
            {
                span.Tags.Add(new Tag { Key = attr.Key, StringValue = attr.Value.ToString() });
            }

            foreach (var evt in data.Events.Events)
            {
                var fields = new List<Tag>();
                fields.Add(new Tag { Key = evt.Event.Name, StringValue = evt.Event.Attributes.ToString() });
                span.Logs.Add(new Log { Timestamp = evt.Timestamp, Fields = fields });
            }

            return span;
        }

        private Task SendSpansAsync(LightStepReport report)
        {
            var requestUri = this.options.Satellite;
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            var jsonReport = JsonConvert.SerializeObject(report);
            request.Content = new StringContent(jsonReport, Encoding.UTF8, "application/json");
            return this.PostSpans(this.httpClient, request);
        }

        private async Task PostSpans(HttpClient client, HttpRequestMessage request)
        {
            using (var res = await client.SendAsync(request))
            {
                if (res.StatusCode != HttpStatusCode.OK && res.StatusCode != HttpStatusCode.Accepted)
                {
                    var sc = (int)res.StatusCode;
                }
            }
        }
    }
}
