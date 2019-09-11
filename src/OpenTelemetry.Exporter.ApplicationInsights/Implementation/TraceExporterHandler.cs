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

namespace OpenTelemetry.Exporter.ApplicationInsights.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    internal class TraceExporterHandler : IHandler
    {
        private readonly TelemetryClient telemetryClient;
        private readonly string serviceEndpoint;

        public TraceExporterHandler(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
            this.serviceEndpoint = telemetryConfiguration.TelemetryChannel.EndpointAddress;
        }

        public Task ExportAsync(IEnumerable<SpanData> spanDataList)
        {
            foreach (var span in spanDataList)
            {
                bool shouldExport = true;
                string httpUrlAttr = null;

                foreach (var attr in span.Attributes.AttributeMap)
                {
                    if (attr.Key == "http.url")
                    {
                        httpUrlAttr = attr.Value.ToString();
                        if (httpUrlAttr == this.serviceEndpoint)
                        {
                            shouldExport = false;
                            break;
                        }
                    }
                }

                if (!shouldExport)
                {
                    continue;
                }

                this.ExtractGenericProperties(
                    span,
                    out var name,
                    out var resultCode,
                    out var statusDescription,
                    out var traceId,
                    out var spanId,
                    out var parentId,
                    out var tracestate,
                    out var success,
                    out var duration);

                // BUILDING resulting telemetry
                OperationTelemetry result;
                if (span.Kind == SpanKind.Client || span.Kind == SpanKind.Internal || span.Kind == SpanKind.Producer)
                {
                    var resultD = new DependencyTelemetry();
                    if (span.Kind == SpanKind.Internal)
                    {
                        resultD.Type = "InProc";
                    }

                    result = resultD;
                }
                else
                {
                    result = new RequestTelemetry();
                }

                string data = null;
                string target = null;
                string type = null;
                string userAgent = null;

                string errorAttr = null;
                string httpStatusCodeAttr = null;
                string httpMethodAttr = null;
                string httpPathAttr = null;
                string httpHostAttr = null;

                string httpUserAgentAttr = null;
                string httpRouteAttr = null;
                string httpPortAttr = null;

                foreach (var attr in span.Attributes.AttributeMap)
                {
                    switch (attr.Key)
                    {
                        case "error":
                            errorAttr = attr.Value.ToString();
                            break;
                        case "http.method":
                            httpMethodAttr = attr.Value.ToString();
                            break;
                        case "http.path":
                            httpPathAttr = attr.Value.ToString();
                            break;
                        case "http.host":
                            httpHostAttr = attr.Value.ToString();
                            break;
                        case "http.status_code":
                            httpStatusCodeAttr = attr.Value.ToString();
                            break;
                        case "http.user_agent":
                            httpUserAgentAttr = attr.Value.ToString();
                            break;
                        case "http.route":
                            httpRouteAttr = attr.Value.ToString();
                            break;
                        case "http.port":
                            httpPortAttr = attr.Value.ToString();
                            break;
                        default:
                            var value = attr.Value.ToString();

                            AddPropertyWithAdjustedName(result.Properties, attr.Key, value);

                            break;
                    }
                }

                this.OverwriteFieldsForHttpSpans(
                    httpMethodAttr,
                    httpUrlAttr,
                    httpHostAttr,
                    httpPathAttr,
                    httpStatusCodeAttr,
                    httpUserAgentAttr,
                    httpRouteAttr,
                    httpPortAttr,
                    ref name,
                    ref resultCode,
                    ref data,
                    ref target,
                    ref type,
                    ref userAgent);

                if (result is DependencyTelemetry dependency)
                {
                    dependency.Data = data;
                    dependency.Target = target;
                    dependency.Data = data;
                    dependency.ResultCode = resultCode;

                    if (string.IsNullOrEmpty(dependency.Type))
                    {
                        dependency.Type = type;
                    }
                }
                else if (result is RequestTelemetry request)
                {
                    if (Uri.TryCreate(data, UriKind.RelativeOrAbsolute, out var url))
                    {
                        request.Url = url;
                    }

                    request.ResponseCode = resultCode;
                }

                if (span.Links.Links.Count() != 0)
                {
                    var linksJson = new StringBuilder();
                    linksJson.Append('[');
                    foreach (var link in span.Links.Links)
                    {
                        var linkTraceId = link.Context.TraceId.ToHexString();

                        // avoiding json serializers for now because of extra dependency.
                        // System.Text.Json is starting at 4.6.1 while exporter is 4.6
                        // also serialization is trivial and looks like `links` property with json blob
                        // [{"operation_Id":"5eca8b153632494ba00f619d6877b134","id":"|5eca8b153632494ba00f619d6877b134.d4c1279b6e7b7c47."},
                        //  {"operation_Id":"ff28988d0776b44f9ca93352da126047","id":"|ff28988d0776b44f9ca93352da126047.bf4fa4855d161141."}]
                        linksJson
                            .Append('{')
                            .Append("\"operation_Id\":")
                            .Append('\"')
                            .Append(linkTraceId)
                            .Append('\"')
                            .Append(',');
                        linksJson
                            .Append("\"id\":")
                            .Append('\"')
                            .Append('|')
                            .Append(linkTraceId)
                            .Append('.')
                            .Append(link.Context.SpanId.ToHexString())
                            .Append('.')
                            .Append('\"');

                        // we explicitly ignore sampling flag, tracestate and attributes at this point.
                        linksJson.Append("},");
                    }

                    linksJson.Append("]");
                    result.Properties["links"] = linksJson.ToString();
                }

                foreach (var t in span.Events.Events)
                {
                    var log = new TraceTelemetry(t.Event.Name);

                    if (t.Timestamp != null)
                    {
                        log.Timestamp = t.Timestamp;
                    }

                    foreach (var attr in t.Event.Attributes)
                    {
                        var value = attr.Value.ToString();

                        AddPropertyWithAdjustedName(log.Properties, attr.Key, value);
                    }

                    log.Context.Operation.Id = traceId;
                    log.Context.Operation.ParentId = string.Concat("|", traceId, ".", spanId, ".");

                    this.telemetryClient.Track(log);
                }

                this.OverwriteErrorAttribute(errorAttr, ref success);

                result.Success = success;
                if (statusDescription != null)
                {
                    AddPropertyWithAdjustedName(result.Properties, "statusDescription", statusDescription);
                }

                result.Timestamp = span.StartTimestamp;
                result.Name = name;
                result.Context.Operation.Id = traceId;
                result.Context.User.UserAgent = userAgent;

                if (parentId != null)
                {
                    result.Context.Operation.ParentId = string.Concat("|", traceId, ".", parentId, ".");
                }

                // TODO: this concatenation is required for Application Insights backward compatibility reasons
                result.Id = string.Concat("|", traceId, ".", spanId, ".");

                foreach (var ts in tracestate.Entries)
                {
                    result.Properties[ts.Key] = ts.Value;
                }

                result.Duration = duration;

                // TODO: deal with those:
                // span.ChildSpanCount
                // span.Context.TraceOptions;

                this.telemetryClient.Track(result);
            }

            return Task.CompletedTask;
        }

        private static void AddPropertyWithAdjustedName(IDictionary<string, string> props, string name, string value)
        {
            var n = name;
            var i = 0;
            while (props.ContainsKey(n))
            {
                n = name + "_" + i;
                ++i;
            }

            props.Add(n, value);
        }

        private void ExtractGenericProperties(SpanData span,  out string name, out string resultCode, out string statusDescription, out string traceId, out string spanId, out string parentId, out Tracestate tracestate, out bool? success, out TimeSpan duration)
        {
            name = span.Name;

            statusDescription = null;

            traceId = span.Context.TraceId.ToHexString();
            spanId = span.Context.SpanId.ToHexString();
            parentId = null;
            if (span.ParentSpanId != default)
            {
                parentId = span.ParentSpanId.ToHexString();
            }

            resultCode = null;
            success = null;
            if (span.Status.IsValid)
            {
                resultCode = ((int)span.Status.CanonicalCode).ToString();
                success = span.Status.IsOk;
                if (!string.IsNullOrEmpty(span.Status.Description))
                {
                    statusDescription = span.Status.Description;
                }
            }

            tracestate = span.Context.Tracestate;
            duration = span.EndTimestamp - span.StartTimestamp;
        }

        private void OverwriteErrorAttribute(string errorAttr, ref bool? success)
        {
            if (errorAttr != null)
            {
                success = errorAttr.ToLowerInvariant() != "true";
            }
        }

        private void OverwriteFieldsForHttpSpans(
            string httpMethodAttr,
            string httpUrlAttr,
            string httpHostAttr,
            string httpPathAttr,
            string httpStatusCodeAttr,
            string httpUserAgentAttr,
            string httpRouteAttr,
            string httpPortAttr,
            ref string name,
            ref string resultCode,
            ref string data,
            ref string target,
            ref string type,
            ref string userAgent)
        {
            if (httpStatusCodeAttr != null)
            {
                resultCode = httpStatusCodeAttr.ToString(CultureInfo.InvariantCulture);
                type = "Http";
            }

            Uri url = null;

            if (httpUrlAttr != null)
            {
                var urlString = httpUrlAttr;
                Uri.TryCreate(urlString, UriKind.RelativeOrAbsolute, out url);
            }

            string httpMethod = null;
            string httpPath = null;
            string httpHost = null;
            string httpRoute = null;
            string httpPort = null;

            if (httpMethodAttr != null)
            {
                httpMethod = httpMethodAttr;
                type = "Http";
            }

            if (httpPathAttr != null)
            {
                httpPath = httpPathAttr;
                type = "Http";
            }

            if (httpHostAttr != null)
            {
                httpHost = httpHostAttr;
                type = "Http";
            }

            if (httpUserAgentAttr != null)
            {
                userAgent = httpUserAgentAttr;
                type = "Http";
            }

            if (httpRouteAttr != null)
            {
                httpRoute = httpRouteAttr;
                type = "Http";
            }

            if (httpRouteAttr != null)
            {
                httpRoute = httpRouteAttr;
                type = "Http";
            }

            if (httpPortAttr != null)
            {
                httpPort = httpPortAttr;
                type = "Http";
            }

            // restore optional fields when possible
            if ((httpPathAttr == null) && (url != null))
            {
                if (url.IsAbsoluteUri)
                {
                    httpPath = url.LocalPath;
                }
                else
                {
                    var idx = url.OriginalString.IndexOf('?');
                    if (idx != -1)
                    {
                        httpPath = url.OriginalString.Substring(0, idx);
                    }
                    else
                    {
                        httpPath = url.OriginalString;
                    }
                }
            }

            if (url == null)
            {
                var urlString = string.Empty;
                if (!string.IsNullOrEmpty(httpHost))
                {
                    urlString += "https://" + httpHost;

                    if (!string.IsNullOrEmpty(httpPort))
                    {
                        urlString += ":" + httpPort;
                    }
                }

                if (!string.IsNullOrEmpty(httpPath))
                {
                    if (httpPath[0] != '/')
                    {
                        urlString += '/';
                    }

                    urlString += httpPath;
                }

                if (!string.IsNullOrEmpty(urlString))
                {
                    Uri.TryCreate(urlString, UriKind.RelativeOrAbsolute, out url);
                }
            }

            // overwriting
            if (httpPath != null || httpMethod != null || httpRoute != null)
            {
                if (httpRoute != null)
                {
                    name = (httpMethod + " " + httpRoute).Trim();
                }
                else
                {
                    name = (httpMethod + " " + httpPath).Trim();
                }
            }

            if (url != null)
            {
                data = url.ToString();
            }

            if ((url != null) && url.IsAbsoluteUri)
            {
                target = url.Host;
            }
        }
    }
}
