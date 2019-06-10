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

        public TraceExporterHandler(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        public async Task ExportAsync(IEnumerable<ISpanData> spanDataList)
        {
            await Task.Run(async () =>
            {
                foreach (var span in spanDataList)
                {
                    this.ExtractGenericProperties(
                        span,
                        out var resultKind,
                        out var timestamp,
                        out var name,
                        out var resultCode,
                        out var props,
                        out var traceId,
                        out var spanId,
                        out var parentId,
                        out var tracestate,
                        out var success,
                        out var duration);

                    string data = null;
                    string target = null;
                    string type = null;
                    string userAgent = null;

                    IAttributeValue spanKindAttr = null;
                    IAttributeValue errorAttr = null;
                    IAttributeValue httpStatusCodeAttr = null;
                    IAttributeValue httpMethodAttr = null;
                    IAttributeValue httpPathAttr = null;
                    IAttributeValue httpHostAttr = null;
                    IAttributeValue httpUrlAttr = null;
                    IAttributeValue httpUserAgentAttr = null;
                    IAttributeValue httpRouteAttr = null;
                    IAttributeValue httpPortAttr = null;

                    foreach (var attr in span.Attributes.AttributeMap)
                    {
                        var key = attr.Key;

                        switch (attr.Key)
                        {
                            case "span.kind":
                                spanKindAttr = attr.Value;
                                break;
                            case "error":
                                errorAttr = attr.Value;
                                break;
                            case "http.method":
                                httpMethodAttr = attr.Value;
                                break;
                            case "http.path":
                                httpPathAttr = attr.Value;
                                break;
                            case "http.host":
                                httpHostAttr = attr.Value;
                                break;
                            case "http.url":
                                httpUrlAttr = attr.Value;
                                break;
                            case "http.status_code":
                                httpStatusCodeAttr = attr.Value;
                                break;
                            case "http.user_agent":
                                httpUserAgentAttr = attr.Value;
                                break;
                            case "http.route":
                                httpRouteAttr = attr.Value;
                                break;
                            case "http.port":
                                httpPortAttr = attr.Value;
                                break;
                            default:
                                var value = attr.Value.Match<string>(
                                    (s) => { return s; },
                                    (b) => { return b.ToString(); },
                                    (l) => { return l.ToString(); },
                                    (d) => { return d.ToString(); },
                                    (obj) => { return obj.ToString(); });

                                AddPropertyWithAdjustedName(props, attr.Key, value);

                                break;
                        }
                    }

                    var linkId = 0;
                    foreach (var link in span.Links.Links)
                    {
                        AddPropertyWithAdjustedName(props, "link" + linkId + "_traceId", link.Context.TraceId.ToLowerBase16());
                        AddPropertyWithAdjustedName(props, "link" + linkId + "_spanId", link.Context.SpanId.ToLowerBase16());

                        foreach (var attr in link.Attributes)
                        {
                            AddPropertyWithAdjustedName(props, "link" + linkId + "_" + attr.Key, attr.Value.Match((s) => s, (b) => b.ToString(), (l) => l.ToString(), (d) => d.ToString(), (obj) => obj.ToString()));
                        }

                        ++linkId;
                    }

                    foreach (var t in span.Events.Events)
                    {
                        var log = new TraceTelemetry(t.Event.Name);

                        if (t.Timestamp != null)
                        {
                            var logTimestamp = DateTimeOffset.FromUnixTimeSeconds(t.Timestamp.Seconds);
                            logTimestamp = logTimestamp.Add(TimeSpan.FromTicks(t.Timestamp.Nanos / 100));
                            log.Timestamp = logTimestamp;
                        }

                        foreach (var attr in t.Event.Attributes)
                        {
                            var value = attr.Value.Match<string>(
                                (s) => { return s; },
                                (b) => { return b.ToString(); },
                                (l) => { return l.ToString(); },
                                (d) => { return d.ToString(); },
                                (obj) => { return obj.ToString(); });

                            AddPropertyWithAdjustedName(log.Properties, attr.Key, value);
                        }

                        log.Context.Operation.Id = traceId;
                        log.Context.Operation.ParentId = string.Concat("|", traceId, ".", spanId, ".");

                        this.telemetryClient.Track(log);
                    }

                    this.OverwriteSpanKindFromAttribute(spanKindAttr, ref resultKind);
                    this.OverwriteErrorAttribute(errorAttr, ref success);
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

                    // BUILDING resulting telemetry
                    OperationTelemetry result;
                    if (resultKind == SpanKind.Client || resultKind == SpanKind.Producer)
                    {
                        var resultD = new DependencyTelemetry();
                        resultD.ResultCode = resultCode;
                        resultD.Data = data;
                        resultD.Target = target;
                        resultD.Type = type;

                        result = resultD;
                    }
                    else
                    {
                        var resultR = new RequestTelemetry();
                        resultR.ResponseCode = resultCode;
                        Uri.TryCreate(data, UriKind.RelativeOrAbsolute, out var url);
                        resultR.Url = url;
                        result = resultR;
                    }

                    result.Success = success;

                    result.Timestamp = timestamp;
                    result.Name = name;
                    result.Context.Operation.Id = traceId;
                    result.Context.User.UserAgent = userAgent;

                    foreach (var prop in props)
                    {
                        AddPropertyWithAdjustedName(result.Properties, prop.Key, prop.Value);
                    }

                    if (parentId != null)
                    {
                        result.Context.Operation.ParentId = string.Concat("|", traceId, ".", parentId, ".");
                    }

                    // TODO: I don't understant why this concatanation is required
                    result.Id = string.Concat("|", traceId, ".", spanId, ".");

                    foreach (var ts in tracestate.Entries)
                    {
                        result.Properties[ts.Key] = ts.Value;
                    }

                    result.Duration = duration;

                    // TODO: deal with those:
                    // span.ChildSpanCount
                    // span.Context.IsValid;
                    // span.Context.TraceOptions;

                    this.telemetryClient.Track(result);
                }
            });
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

        private void ExtractGenericProperties(ISpanData span, out SpanKind resultKind, out DateTimeOffset timestamp, out string name, out string resultCode, out IDictionary<string, string> props, out string traceId, out string spanId, out string parentId, out Tracestate tracestate, out bool? success, out TimeSpan duration)
        {
            resultKind = span.Kind;

            // TODO: Should this be a part of generic logic?
            if (resultKind == SpanKind.Internal)
            {
                resultKind = SpanKind.Client;
            }

            // 1 tick is 100 ns
            timestamp = DateTimeOffset.FromUnixTimeSeconds(span.StartTimestamp.Seconds);
            timestamp = timestamp.Add(TimeSpan.FromTicks(span.StartTimestamp.Nanos / 100));

            name = span.Name;

            props = new Dictionary<string, string>();

            traceId = span.Context.TraceId.ToLowerBase16();
            spanId = span.Context.SpanId.ToLowerBase16();
            parentId = null;
            if (span.ParentSpanId != null && span.ParentSpanId.IsValid)
            {
                parentId = span.ParentSpanId.ToLowerBase16();
            }

            resultCode = null;
            success = null;
            if (span.Status != null)
            {
                resultCode = ((int)span.Status.CanonicalCode).ToString();
                success = span.Status.IsOk;
                if (!string.IsNullOrEmpty(span.Status.Description))
                {
                    props["statusDescription"] = span.Status.Description;
                }
            }

            tracestate = span.Context.Tracestate;

            var durationTs = span.EndTimestamp.SubtractTimestamp(span.StartTimestamp);
            duration = TimeSpan.FromTicks((durationTs.Seconds * TimeSpan.TicksPerSecond) + (durationTs.Nanos / 100));
        }

        private void OverwriteSpanKindFromAttribute(IAttributeValue spanKindAttr, ref SpanKind resultKind)
        {
            // override span kind with attribute named span.kind
            if (spanKindAttr != null)
            {
                var kind = spanKindAttr.Match((s) => s, null, null, null, null);

                switch (kind.ToLower(CultureInfo.InvariantCulture))
                {
                    case "server":
                        resultKind = SpanKind.Server;
                        break;
                    case "client":
                        resultKind = SpanKind.Client;
                        break;
                    case "producer":
                        resultKind = SpanKind.Producer;
                        break;
                    case "consumer":
                        resultKind = SpanKind.Consumer;
                        break;
                    default:
                        resultKind = SpanKind.Internal;
                        break;
                }
            }
        }

        private void OverwriteErrorAttribute(IAttributeValue errorAttr, ref bool? success)
        {
            if (errorAttr != null)
            {
                success = errorAttr.Match((s) => !(s == "true"), (b) => !b, null, null, null);
            }
        }

        private void OverwriteFieldsForHttpSpans(
            IAttributeValue httpMethodAttr,
            IAttributeValue httpUrlAttr,
            IAttributeValue httpHostAttr,
            IAttributeValue httpPathAttr,
            IAttributeValue httpStatusCodeAttr,
            IAttributeValue httpUserAgentAttr,
            IAttributeValue httpRouteAttr,
            IAttributeValue httpPortAttr,
            ref string name,
            ref string resultCode,
            ref string data,
            ref string target,
            ref string type,
            ref string userAgent)
        {
            if (httpStatusCodeAttr != null)
            {
                resultCode = httpStatusCodeAttr.Match((s) => s, null, (l) => l.ToString(CultureInfo.InvariantCulture), null, null);
                type = "Http";
            }

            Uri url = null;

            if (httpUrlAttr != null)
            {
                var urlString = httpUrlAttr.Match((s) => s, null, null, null, null);
                Uri.TryCreate(urlString, UriKind.RelativeOrAbsolute, out url);
           }

            string httpMethod = null;
            string httpPath = null;
            string httpHost = null;
            string httpRoute = null;
            string httpPort = null;

            if (httpMethodAttr != null)
            {
                httpMethod = httpMethodAttr.Match((s) => s, null, null, null, null);
                type = "Http";
            }

            if (httpPathAttr != null)
            {
                httpPath = httpPathAttr.Match((s) => s, null, null, null, null);
                type = "Http";
            }

            if (httpHostAttr != null)
            {
                httpHost = httpHostAttr.Match((s) => s, null, null, null, null);
                type = "Http";
            }

            if (httpUserAgentAttr != null)
            {
                userAgent = httpUserAgentAttr.Match((s) => s, null, null, null, null);
                type = "Http";
            }

            if (httpRouteAttr != null)
            {
                httpRoute = httpRouteAttr.Match((s) => s, null, null, null, null);
                type = "Http";
            }

            if (httpRouteAttr != null)
            {
                httpRoute = httpRouteAttr.Match((s) => s, null, null, null, null);
                type = "Http";
            }

            if (httpPortAttr != null)
            {
                httpPort = httpPortAttr.Match((s) => s, null, (l) => l.ToString(), null, null);
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
                    int idx = url.OriginalString.IndexOf('?');
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
                string urlString = string.Empty;
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
