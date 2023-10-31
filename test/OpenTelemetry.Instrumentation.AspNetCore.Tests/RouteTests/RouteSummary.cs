// <copyright file="RouteSummary.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RouteTests;

public class RouteSummary
{
    [JsonPropertyName("RoutePattern.RawText")]
    public string? RawText { get; set; }

    [JsonPropertyName("IRouteDiagnosticsMetadata.Route")]
    public string? RouteDiagnosticMetadata { get; set; }

    [JsonPropertyName("HttpContext.GetRouteData()")]
    public IDictionary<string, string?>? RouteData { get; set; }

    [JsonPropertyName("ActionDescriptor")]
    public ActionDescriptorSummary? ActionDescriptorSummary { get; set; }
}

public class ActionDescriptorSummary
{
    public ActionDescriptorSummary()
    {
    }

    public ActionDescriptorSummary(ActionDescriptor actionDescriptor)
    {
        this.AttributeRouteInfo = actionDescriptor.AttributeRouteInfo?.Template;

        this.ActionParameters = new List<string>();
        foreach (var item in actionDescriptor.Parameters)
        {
            this.ActionParameters.Add(item.Name);
        }

        if (actionDescriptor is PageActionDescriptor pad)
        {
            this.PageActionDescriptorSummary = new PageActionDescriptorSummary(pad.RelativePath, pad.ViewEnginePath);
        }

        if (actionDescriptor is ControllerActionDescriptor cad)
        {
            this.ControllerActionDescriptorSummary = new ControllerActionDescriptorSummary(cad.ControllerName, cad.ActionName);
        }
    }

    [JsonPropertyName("AttributeRouteInfo.Template")]
    public string? AttributeRouteInfo { get; set; }

    [JsonPropertyName("Parameters")]
    public IList<string>? ActionParameters { get; set; }

    [JsonPropertyName("ControllerActionDescriptor")]
    public ControllerActionDescriptorSummary? ControllerActionDescriptorSummary { get; set; }

    [JsonPropertyName("PageActionDescriptor")]
    public PageActionDescriptorSummary? PageActionDescriptorSummary { get; set; }
}

public class ControllerActionDescriptorSummary
{
    public ControllerActionDescriptorSummary()
    {
    }

    public ControllerActionDescriptorSummary(string controllerName, string actionName)
    {
        this.ControllerActionDescriptorControllerName = controllerName;
        this.ControllerActionDescriptorActionName = actionName;
    }

    [JsonPropertyName("ControllerName")]
    public string ControllerActionDescriptorControllerName { get; set; }

    [JsonPropertyName("ActionName")]
    public string ControllerActionDescriptorActionName { get; set; }
}

public class PageActionDescriptorSummary
{
    public PageActionDescriptorSummary()
    {
    }

    public PageActionDescriptorSummary(string relativePath, string viewEnginePath)
    {
        this.PageActionDescriptorRelativePath = relativePath;
        this.PageActionDescriptorViewEnginePath = viewEnginePath;
    }

    [JsonPropertyName("RelativePath")]
    public string PageActionDescriptorRelativePath { get; set; }

    [JsonPropertyName("ViewEnginePath")]
    public string PageActionDescriptorViewEnginePath { get; set; }
}
