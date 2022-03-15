// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CustomizingTheSdk;

public class Program
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
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()

            // The following adds subscription to activities from Activity Source
            // named "MyCompany.MyProduct.MyLibrary" only.
            .AddSource("MyCompany.MyProduct.MyLibrary")

            // The following adds subscription to activities from all Activity Sources
            // whose name starts with "AbcCompany.XyzProduct.".
            .AddSource("AbcCompany.XyzProduct.*")
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
    }
}
