// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    public static RouteInfo Current { get; set; } = new();

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
