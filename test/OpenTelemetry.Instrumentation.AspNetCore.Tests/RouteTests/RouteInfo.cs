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
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace RouteTests;

public class RouteInfo
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public RouteInfo()
    {
        this.DebugInfo = new DebugInfo();
    }

    public string? HttpMethod { get; set; }

    public string? Path { get; set; }

    public string? HttpRouteByRawText => this.DebugInfo.RawText;

    public string HttpRouteByControllerActionAndParameters
    {
        get
        {
            var condition = !string.IsNullOrEmpty(this.DebugInfo.ControllerActionDescriptorActionName)
                && !string.IsNullOrEmpty(this.DebugInfo.ControllerActionDescriptorActionName)
                && this.DebugInfo.ActionParameters != null;

            if (!condition)
            {
                return string.Empty;
            }

            var paramList = string.Join(string.Empty, this.DebugInfo.ActionParameters!.Select(p => $"/{{{p}}}"));
            return $"{this.DebugInfo.ControllerActionDescriptorControllerName}/{this.DebugInfo.ControllerActionDescriptorActionName}{paramList}";
        }
    }

    public string HttpRouteByActionDescriptor
    {
        get
        {
            var result = string.Empty;

            var hasControllerActionDescriptor = this.DebugInfo.ControllerActionDescriptorControllerName != null
                && this.DebugInfo.ControllerActionDescriptorActionName != null;

            var hasPageActionDescriptor = this.DebugInfo.PageActionDescriptorRelativePath != null
                && this.DebugInfo.PageActionDescriptorViewEnginePath != null;

            if (this.DebugInfo.RawText != null && hasControllerActionDescriptor)
            {
                var controllerRegex = new System.Text.RegularExpressions.Regex(@"\{controller=.*?\}+?");
                var actionRegex = new System.Text.RegularExpressions.Regex(@"\{action=.*?\}+?");
                result = controllerRegex.Replace(this.DebugInfo.RawText, this.DebugInfo.ControllerActionDescriptorControllerName!);
                result = actionRegex.Replace(result, this.DebugInfo.ControllerActionDescriptorActionName!);
            }
            else if (this.DebugInfo.RawText != null && hasPageActionDescriptor)
            {
                result = this.DebugInfo.PageActionDescriptorViewEnginePath!;
            }

            return result;
        }
    }

    public DebugInfo DebugInfo { get; set; }

    public void SetValues(HttpContext context)
    {
        this.HttpMethod = context.Request.Method;
        this.Path = $"{context.Request.Path}{context.Request.QueryString}";
        var endpoint = context.GetEndpoint();
        this.DebugInfo.RawText = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
#if NET8_0_OR_GREATER
        this.DebugInfo.RouteDiagnosticMetadata = endpoint?.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()?.Route;
#endif
        this.DebugInfo.RouteData = new Dictionary<string, string?>();
        foreach (var value in context.GetRouteData().Values)
        {
            this.DebugInfo.RouteData[value.Key] = value.Value?.ToString();
        }
    }

    public void SetValues(ActionDescriptor actionDescriptor)
    {
        this.DebugInfo.AttributeRouteInfo = actionDescriptor.AttributeRouteInfo?.Template;

        this.DebugInfo.ActionParameters = new List<string>();
        foreach (var item in actionDescriptor.Parameters)
        {
            this.DebugInfo.ActionParameters.Add(item.Name);
        }

        if (actionDescriptor is PageActionDescriptor pad)
        {
            this.DebugInfo.PageActionDescriptorRelativePath = pad.RelativePath;
            this.DebugInfo.PageActionDescriptorViewEnginePath = pad.ViewEnginePath;
        }

        if (actionDescriptor is ControllerActionDescriptor cad)
        {
            this.DebugInfo.ControllerActionDescriptorControllerName = cad.ControllerName;
            this.DebugInfo.ControllerActionDescriptorActionName = cad.ActionName;
        }
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}
