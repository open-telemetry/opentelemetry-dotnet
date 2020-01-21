// <copyright file="ApplicationInsightsTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ApplicationInsights
{
    /// <summary>
    /// Application Insights trace exporter.
    /// </summary>
    public class ApplicationInsightsTraceExporter : SpanExporter, IDisposable
    {
        private readonly TelemetryClient telemetryClient;
        private readonly string serviceEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInsightsTraceExporter"/> class.
        /// </summary>
        /// <param name="telemetryConfiguration">Telemetry configuration to use.</param>
        public ApplicationInsightsTraceExporter(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
            this.telemetryClient.Context.GetInternalContext().SdkVersion = "ot:" + GetAssemblyVersion();
            this.serviceEndpoint = telemetryConfiguration.TelemetryChannel.EndpointAddress;
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> spanDataList, CancellationToken cancellationToken)
        {
            foreach (var span in spanDataList)
            {
                bool shouldExport = true;
                string httpUrlAttr = null;

                foreach (var attr in span.Attributes)
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
                    out var success,
                    out var duration,
                    out var roleName,
                    out var roleInstance,
                    out var version);

                // BUILDING resulting telemetry
                OperationTelemetry result;
                if (span.Kind == SpanKind.Client || span.Kind == SpanKind.Internal || span.Kind == SpanKind.Producer)
                {
                    result = new DependencyTelemetry
                    {
                        Name = name,
                        ResultCode = resultCode,
                        Type = span.Kind == SpanKind.Internal ? "InProc" :
                               span.Kind == SpanKind.Producer ? "Queue Message" : null,
                    };
                }
                else
                {
                    result = new RequestTelemetry
                    {
                        Name = name,
                        ResponseCode = resultCode,
                    };
                }

                string component = this.GetComponent(span);

                switch (component)
                {
                    case "http":
                        this.SetHttpProperties(
                            span,
                            result,
                            httpUrlAttr);
                        break;
                    case "eventhubs":
                    case "Microsoft.EventHub":
                        this.SetEventHubsProperties(span, result);
                        break;
                    default:

                        if (result is DependencyTelemetry dependency && dependency.Type == null)
                        {
                            dependency.Type = component;
                        }

                        foreach (var attribute in span.Attributes)
                        {
                            AddPropertyWithAdjustedName(result.Properties, attribute.Key, attribute.Value.ToString());
                        }

                        break;
                }

                if (span.Links.Count() != 0)
                {
                    var linksJson = new StringBuilder();
                    linksJson.Append('[');
                    foreach (var link in span.Links)
                    {
                        var linkTraceId = link.Context.TraceId.ToHexString();

                        // avoiding json serializers for now because of extra dependency.
                        // System.Text.Json is starting at 4.6.1 while exporter is 4.6
                        // also serialization is trivial and looks like `links` property with json blob
                        // [{"operation_Id":"5eca8b153632494ba00f619d6877b134","id":"d4c1279b6e7b7c47"},
                        //  {"operation_Id":"ff28988d0776b44f9ca93352da126047","id":"bf4fa4855d161141"}]
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
                            .Append(link.Context.SpanId.ToHexString())
                            .Append('\"');

                        // we explicitly ignore sampling flag, tracestate and attributes at this point.
                        linksJson.Append("},");
                    }

                    // trim last comma, json does not support it
                    if (linksJson.Length > 0)
                    {
                        linksJson.Remove(linksJson.Length - 1, 1);
                    }

                    linksJson.Append("]");
                    result.Properties["_MS.links"] = linksJson.ToString();
                }

                foreach (var t in span.Events)
                {
                    var log = new TraceTelemetry(t.Name);

                    if (t.Timestamp != null)
                    {
                        log.Timestamp = t.Timestamp;
                    }

                    foreach (var attr in t.Attributes)
                    {
                        var value = attr.Value.ToString();

                        AddPropertyWithAdjustedName(log.Properties, attr.Key, value);
                    }

                    log.Context.Operation.Id = traceId;
                    log.Context.Operation.ParentId = spanId;
                    log.Context.Cloud.RoleName = roleName;
                    log.Context.Cloud.RoleInstance = roleInstance;
                    log.Context.Component.Version = version;

                    this.telemetryClient.Track(log);
                }

                result.Success = success;
                if (statusDescription != null)
                {
                    AddPropertyWithAdjustedName(result.Properties, "statusDescription", statusDescription);
                }

                result.Timestamp = span.StartTimestamp;
                result.Context.Operation.Id = traceId;
                result.Context.Cloud.RoleName = roleName;
                result.Context.Cloud.RoleInstance = roleInstance;
                result.Context.Component.Version = version;

                if (parentId != null)
                {
                    result.Context.Operation.ParentId = parentId;
                }

                result.Id = spanId;

                foreach (var ts in span.Context.Tracestate)
                {
                    result.Properties[ts.Key] = ts.Value;
                }

                result.Duration = duration;

                // TODO: deal with those:
                // span.ChildSpanCount
                // span.Context.TraceOptions;

                this.telemetryClient.Track(result);
            }

            return Task.FromResult(ExportResult.Success);
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            // TODO cancellation support
            this.telemetryClient.Flush();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.ShutdownAsync(CancellationToken.None).ContinueWith(_ => { }).Wait();
        }

        private static void AddPropertyWithAdjustedName(IDictionary<string, string> props, string name, string value)
        {
            if (name == "component" || name == "az.namespace" || name == "kind")
            {
                // these attributes are reflected in telemetry types and should not be populated.
                return;
            }

            var n = name;
            var i = 0;
            while (props.ContainsKey(n))
            {
                n = name + "_" + i;
                ++i;
            }

            props.Add(n, value);
        }

        private static string GetAssemblyVersion()
        {
            try
            {
                return typeof(ApplicationInsightsTraceExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyFileVersionAttribute>()
                                      .First()
                                      .Version;
            }
            catch (Exception)
            {
                return "0.0.0";
            }
        }

        private void ExtractGenericProperties(
            SpanData span,
            out string name,
            out string resultCode,
            out string statusDescription,
            out string traceId,
            out string spanId,
            out string parentId,
            out bool? success,
            out TimeSpan duration,
            out string roleName,
            out string roleInstance,
            out string serviceVersion)
        {
            name = span.Name;

            roleName = null;
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
                resultCode = span.Status.CanonicalCode.ToString();
                success = span.Status.IsOk;
                if (!string.IsNullOrEmpty(span.Status.Description))
                {
                    statusDescription = span.Status.Description;
                }
            }

            duration = span.EndTimestamp - span.StartTimestamp;

            string serviceName = null;
            string serviceNamespace = null;
            serviceVersion = null;
            roleInstance = null;

            foreach (var attribute in span.LibraryResource.Attributes)
            {
                if (attribute.Key == "service.name" && attribute.Value is string)
                {
                    serviceName = (string)attribute.Value;
                }
                else if (attribute.Key == "service.namespace" && attribute.Value is string)
                {
                    serviceNamespace = (string)attribute.Value;
                }
                else if (attribute.Key == "service.version" && attribute.Value is string)
                {
                    serviceVersion = (string)attribute.Value;
                }
                else if (attribute.Key == "service.instance.id" && attribute.Value is string)
                {
                    roleInstance = (string)attribute.Value;
                }
            }

            if (serviceName != null && serviceNamespace != null)
            {
                roleName = string.Concat(serviceNamespace, ".", serviceName);
            }
            else
            {
                roleName = serviceName;
            }
        }

        private void SetHttpProperties(SpanData span, OperationTelemetry telemetry, string httpUrlAttr)
        {
            string httpStatusCodeAttr = null;
            string httpMethodAttr = null;
            string httpPathAttr = null;
            string httpHostAttr = null;

            string httpRouteAttr = null;
            string httpPortAttr = null;

            foreach (var attr in span.Attributes)
            {
                switch (attr.Key)
                {
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
                        telemetry.Context.User.UserAgent = attr.Value.ToString();
                        break;
                    case "http.route":
                        httpRouteAttr = attr.Value.ToString();
                        break;
                    case "http.port":
                        httpPortAttr = attr.Value.ToString();
                        break;
                    case "http.url":
                        // break without doing anything - this will prevent adding url to custom property bag.
                        // httpUrlAttr is already populated.
                        break;
                    default:
                        AddPropertyWithAdjustedName(telemetry.Properties, attr.Key, attr.Value.ToString());
                        break;
                }
            }

            string resultCode = null;
            if (httpStatusCodeAttr != null)
            {
                resultCode = httpStatusCodeAttr.ToString(CultureInfo.InvariantCulture);
            }

            Uri url = null;
            if (httpUrlAttr != null)
            {
                Uri.TryCreate(httpUrlAttr, UriKind.RelativeOrAbsolute, out url);
            }

            string httpMethod = null;
            string httpPath = null;
            string httpHost = null;
            string httpRoute = null;
            string httpPort = null;

            if (httpMethodAttr != null)
            {
                httpMethod = httpMethodAttr;
            }

            if (httpPathAttr != null)
            {
                httpPath = httpPathAttr;
            }

            if (httpHostAttr != null)
            {
                httpHost = httpHostAttr;
            }

            if (httpRouteAttr != null)
            {
                httpRoute = httpRouteAttr;
            }

            if (httpRouteAttr != null)
            {
                httpRoute = httpRouteAttr;
            }

            if (httpPortAttr != null)
            {
                httpPort = httpPortAttr;
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
                    telemetry.Name = (httpMethod + " " + httpRoute).Trim();
                }
                else
                {
                    telemetry.Name = (httpMethod + " " + httpPath).Trim();
                }
            }

            if (telemetry is DependencyTelemetry dependency)
            {
                if (url != null)
                {
                    dependency.Data = url.OriginalString;
                    dependency.Target = url.IsAbsoluteUri ? url.Authority : null;
                }
                else
                {
                    dependency.Data = null;
                    dependency.Target = null;
                }

                dependency.ResultCode = resultCode;

                if (string.IsNullOrEmpty(dependency.Type))
                {
                    dependency.Type = "Http";
                }
            }
            else if (telemetry is RequestTelemetry request)
            {
                if (url != null)
                {
                    request.Url = url;
                }

                request.ResponseCode = resultCode;
            }
        }

        private void SetEventHubsProperties(SpanData span, OperationTelemetry telemetry)
        {
            string endpoint = null;
            string queueName = null;

            foreach (var attribute in span.Attributes)
            {
                if (attribute.Key == "peer.address" && attribute.Value is string)
                {
                    endpoint = (string)attribute.Value;
                }
                else if (attribute.Key == "message_bus.destination")
                {
                    queueName = (string)attribute.Value;
                }
                else
                {
                    AddPropertyWithAdjustedName(telemetry.Properties, attribute.Key, attribute.Value.ToString());
                }
            }

            if (endpoint != null && queueName != null)
            {
                if (telemetry is DependencyTelemetry dependency)
                {
                    // Target uniquely identifies the resource, we use both: queueName and endpoint
                    // with schema used for SQL-dependencies
                    dependency.Target = string.Concat(endpoint, " | ", queueName);
                    dependency.Type = "Azure Event Hubs";
                }
                else if (telemetry is RequestTelemetry request)
                {
                    request.Source = string.Concat(endpoint, " | ", queueName);
                }
            }
        }

        private string GetComponent(SpanData span)
        {
            foreach (var attr in span.Attributes)
            {
                if (attr.Key == "component" && attr.Value is string strValue)
                {
                    return strValue;
                }

                if (attr.Key == "az.namespace" && attr.Value is string azNamespace)
                {
                    return azNamespace;
                }

                if (attr.Key.StartsWith("http."))
                {
                    return "http";
                }
            }

            return null;
        }
    }
}
