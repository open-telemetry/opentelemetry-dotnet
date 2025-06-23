// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CustomizingTheSdk;

internal static class Program
{
    private static readonly ActivitySource MyLibraryActivitySource = new(
        "MyCompany.MyProduct.MyLibrary");

    private static readonly ActivitySource ComponentAActivitySource = new(
        "AbcCompany.XyzProduct.ComponentA");

    private static readonly ActivitySource ComponentBActivitySource = new(
        "AbcCompany.XyzProduct.ComponentB");

    private static readonly ActivitySource SomeOtherActivitySource = new(
        "SomeCompany.SomeProduct.SomeComponent");

    public static void Main()
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()

            // The following adds subscription to activities from Activity Source
            // named "MyCompany.MyProduct.MyLibrary" only.
            .AddSource("MyCompany.MyProduct.MyLibrary")

            // The following adds subscription to activities from all Activity Sources
            // whose name starts with "AbcCompany.XyzProduct.".
            .AddSource("AbcCompany.XyzProduct.*")
            .ConfigureResource(resource => resource.AddAttributes(new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("static-attribute1", "v1"),
                    new KeyValuePair<string, object>("static-attribute2", "v2"),
                }))
            .ConfigureResource(resource => resource.AddService("MyServiceName"))
            .AddConsoleExporter()
            .Build();

        // This activity source is enabled.
        using (var activity = MyLibraryActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
        }

        // This activity source is enabled through wild card "AbcCompany.XyzProduct.*"
        using (var activity = ComponentAActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
        }

        // This activity source is enabled through wild card "AbcCompany.XyzProduct.*"
        using (var activity = ComponentBActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
        }

        // This activity source is not enabled, so activity will
        // be null here.
        using (var activity = SomeOtherActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
        }

        tracerProvider.Dispose();
    }
}
