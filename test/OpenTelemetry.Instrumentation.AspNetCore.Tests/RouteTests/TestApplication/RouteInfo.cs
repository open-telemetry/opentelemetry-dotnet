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

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Metadata;
#endif
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace RouteTests.TestApplication;

public class RouteInfo
{
    public string? HttpMethod { get; set; }

    public string? Path { get; set; }

    [JsonPropertyName("RoutePattern.RawText")]
    public string? RawText { get; set; }

    [JsonPropertyName("IRouteDiagnosticsMetadata.Route")]
    public string? RouteDiagnosticMetadata { get; set; }

    [JsonPropertyName("HttpContext.GetRouteData()")]
    public IDictionary<string, string?>? RouteData { get; set; }

    public ActionDescriptorInfo? ActionDescriptor { get; set; }

    public void SetValues(HttpContext context)
    {
        this.HttpMethod = context.Request.Method;
        this.Path = $"{context.Request.Path}{context.Request.QueryString}";
        var endpoint = context.GetEndpoint();
        this.RawText = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
#if NET8_0_OR_GREATER
        this.RouteDiagnosticMetadata = endpoint?.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()?.Route;
#endif
        this.RouteData = new Dictionary<string, string?>();
        foreach (var value in context.GetRouteData().Values)
        {
            this.RouteData[value.Key] = value.Value?.ToString();
        }
    }

    public void SetValues(ActionDescriptor actionDescriptor)
    {
        if (this.ActionDescriptor == null)
        {
            this.ActionDescriptor = new ActionDescriptorInfo(actionDescriptor);
        }
    }

    public class ActionDescriptorInfo
    {
        public ActionDescriptorInfo()
        {
        }

        public ActionDescriptorInfo(ActionDescriptor actionDescriptor)
        {
            this.AttributeRouteInfo = actionDescriptor.AttributeRouteInfo?.Template;

            this.ActionParameters = new List<string>();
            foreach (var item in actionDescriptor.Parameters)
            {
                this.ActionParameters.Add(item.Name);
            }

            if (actionDescriptor is PageActionDescriptor pad)
            {
                this.PageActionDescriptorSummary = new PageActionDescriptorInfo(pad.RelativePath, pad.ViewEnginePath);
            }

            if (actionDescriptor is ControllerActionDescriptor cad)
            {
                this.ControllerActionDescriptorSummary = new ControllerActionDescriptorInfo(cad.ControllerName, cad.ActionName);
            }
        }

        [JsonPropertyName("AttributeRouteInfo.Template")]
        public string? AttributeRouteInfo { get; set; }

        [JsonPropertyName("Parameters")]
        public IList<string>? ActionParameters { get; set; }

        [JsonPropertyName("ControllerActionDescriptor")]
        public ControllerActionDescriptorInfo? ControllerActionDescriptorSummary { get; set; }

        [JsonPropertyName("PageActionDescriptor")]
        public PageActionDescriptorInfo? PageActionDescriptorSummary { get; set; }
    }

    public class ControllerActionDescriptorInfo
    {
        public ControllerActionDescriptorInfo()
        {
        }

        public ControllerActionDescriptorInfo(string controllerName, string actionName)
        {
            this.ControllerActionDescriptorControllerName = controllerName;
            this.ControllerActionDescriptorActionName = actionName;
        }

        [JsonPropertyName("ControllerName")]
        public string ControllerActionDescriptorControllerName { get; set; } = string.Empty;

        [JsonPropertyName("ActionName")]
        public string ControllerActionDescriptorActionName { get; set; } = string.Empty;
    }

    public class PageActionDescriptorInfo
    {
        public PageActionDescriptorInfo()
        {
        }

        public PageActionDescriptorInfo(string relativePath, string viewEnginePath)
        {
            this.PageActionDescriptorRelativePath = relativePath;
            this.PageActionDescriptorViewEnginePath = viewEnginePath;
        }

        [JsonPropertyName("RelativePath")]
        public string PageActionDescriptorRelativePath { get; set; } = string.Empty;

        [JsonPropertyName("ViewEnginePath")]
        public string PageActionDescriptorViewEnginePath { get; set; } = string.Empty;
    }
}
