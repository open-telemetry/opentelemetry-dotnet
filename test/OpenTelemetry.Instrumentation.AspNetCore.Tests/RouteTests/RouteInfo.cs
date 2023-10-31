// <copyright file="RouteInfo.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Text.Json;
using Microsoft.AspNetCore.Http;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Metadata;
#endif
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace RouteTests;

public class RouteInfo
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public RouteInfo()
    {
        this.RouteSummary = new RouteSummary();
    }

    public string? HttpMethod { get; set; }

    public string? Path { get; set; }

    public string? HttpRouteByRawText => this.RouteSummary.RawText;

    public string? HttpRouteByControllerActionAndParameters
    {
        get
        {
            if (this.RouteSummary.ActionDescriptorSummary == null
                || this.RouteSummary.ActionDescriptorSummary.ControllerActionDescriptorSummary == null)
            {
                return string.Empty;
            }

            var controllerName = this.RouteSummary.ActionDescriptorSummary.ControllerActionDescriptorSummary.ControllerActionDescriptorControllerName;
            var actionName = this.RouteSummary.ActionDescriptorSummary.ControllerActionDescriptorSummary.ControllerActionDescriptorActionName;
            var paramList = string.Join(string.Empty, this.RouteSummary.ActionDescriptorSummary.ActionParameters!.Select(p => $"/{{{p}}}"));
            return $"{controllerName}/{actionName}{paramList}";
        }
    }

    public string? HttpRouteByActionDescriptor
    {
        get
        {
            if (this.RouteSummary.RawText == null)
            {
                return null;
            }

            if (this.RouteSummary.ActionDescriptorSummary?.ControllerActionDescriptorSummary != null)
            {
                var controllerRegex = new System.Text.RegularExpressions.Regex(@"\{controller=.*?\}+?");
                var actionRegex = new System.Text.RegularExpressions.Regex(@"\{action=.*?\}+?");
                var controllerName = this.RouteSummary.ActionDescriptorSummary.ControllerActionDescriptorSummary.ControllerActionDescriptorControllerName;
                var actionName = this.RouteSummary.ActionDescriptorSummary.ControllerActionDescriptorSummary.ControllerActionDescriptorActionName;
                var result = controllerRegex.Replace(this.RouteSummary.RawText, controllerName);
                result = actionRegex.Replace(result, actionName);
                return result;
            }

            if (this.RouteSummary.ActionDescriptorSummary?.PageActionDescriptorSummary != null)
            {
                return this.RouteSummary.ActionDescriptorSummary.PageActionDescriptorSummary.PageActionDescriptorViewEnginePath;
            }

            return null;
        }
    }

    public RouteSummary RouteSummary { get; set; }

    public void SetValues(HttpContext context)
    {
        this.HttpMethod = context.Request.Method;
        this.Path = $"{context.Request.Path}{context.Request.QueryString}";
        var endpoint = context.GetEndpoint();
        this.RouteSummary.RawText = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
#if NET8_0_OR_GREATER
        this.RouteSummary.RouteDiagnosticMetadata = endpoint?.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()?.Route;
#endif
        this.RouteSummary.RouteData = new Dictionary<string, string?>();
        foreach (var value in context.GetRouteData().Values)
        {
            this.RouteSummary.RouteData[value.Key] = value.Value?.ToString();
        }
    }

    public void SetValues(ActionDescriptor actionDescriptor)
    {
        if (this.RouteSummary.ActionDescriptorSummary == null)
        {
            this.RouteSummary.ActionDescriptorSummary = new ActionDescriptorSummary(actionDescriptor);
        }
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}
