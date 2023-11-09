// <copyright file="TestApplicationFactory.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace RouteTests.TestApplication;

public enum TestApplicationScenario
{
    /// <summary>
    /// An application that uses conventional routing.
    /// </summary>
    ConventionalRouting,

    /// <summary>
    /// An application that uses attribute routing.
    /// </summary>
    AttributeRouting,

    /// <summary>
    /// A Minimal API application.
    /// </summary>
    MinimalApi,

    /// <summary>
    /// An Razor Pages application.
    /// </summary>
    RazorPages,
}

internal class TestApplicationFactory
{
    private static readonly string AspNetCoreTestsPath = new FileInfo(typeof(RoutingTests)!.Assembly!.Location)!.Directory!.Parent!.Parent!.Parent!.FullName;
    private static readonly string ContentRootPath = Path.Combine(AspNetCoreTestsPath, "RouteTests", "TestApplication");

    public static WebApplication? CreateApplication(TestApplicationScenario config)
    {
        Debug.Assert(Directory.Exists(ContentRootPath), $"Cannot find ContentRootPath: {ContentRootPath}");
        switch (config)
        {
            case TestApplicationScenario.ConventionalRouting:
                return CreateConventionalRoutingApplication();
            case TestApplicationScenario.AttributeRouting:
                return CreateAttributeRoutingApplication();
            case TestApplicationScenario.MinimalApi:
                return CreateMinimalApiApplication();
            case TestApplicationScenario.RazorPages:
                return CreateRazorPagesApplication();
            default:
                throw new ArgumentException($"Invalid {nameof(TestApplicationScenario)}");
        }
    }

    private static WebApplication CreateConventionalRoutingApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = ContentRootPath });
        builder.Services
            .AddControllersWithViews()
            .AddApplicationPart(typeof(RoutingTests).Assembly);

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://[::1]:0");
        app.UseStaticFiles();
        app.UseRouting();

        app.MapAreaControllerRoute(
            name: "AnotherArea",
            areaName: "AnotherArea",
            pattern: "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "MyArea",
            pattern: "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}");

        app.MapControllerRoute(
            name: "FixedRouteWithConstraints",
            pattern: "SomePath/{id}/{num:int}",
            defaults: new { controller = "ConventionalRoute", action = "ActionWithStringParameter" });

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=ConventionalRoute}/{action=Default}/{id?}");

        return app;
    }

    private static WebApplication CreateAttributeRoutingApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(RoutingTests).Assembly);

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://[::1]:0");
        app.MapControllers();

        return app;
    }

    private static WebApplication CreateMinimalApiApplication()
    {
        var builder = WebApplication.CreateBuilder();

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://[::1]:0");

        app.MapGet("/MinimalApi", () => Results.Ok());
        app.MapGet("/MinimalApi/{id}", (int id) => Results.Ok());

#if NET7_0_OR_GREATER
        var api = app.MapGroup("/MinimalApiUsingMapGroup");
        api.MapGet("/", () => Results.Ok());
        api.MapGet("/{id}", (int id) => Results.Ok());
#endif

        return app;
    }

    private static WebApplication CreateRazorPagesApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = ContentRootPath });
        builder.Services
            .AddRazorPages()
            .AddRazorRuntimeCompilation(options =>
            {
                options.FileProviders.Add(new PhysicalFileProvider(ContentRootPath));
            })
            .AddApplicationPart(typeof(RoutingTests).Assembly);

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://[::1]:0");
        app.UseStaticFiles();
        app.UseRouting();
        app.MapRazorPages();

        return app;
    }
}
