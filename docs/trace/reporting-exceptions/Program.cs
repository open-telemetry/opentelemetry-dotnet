// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace ReportingExceptions;

internal static class Program
{
    private static readonly ActivitySource MyActivitySource = new(
        "MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .SetSampler(new AlwaysOnSampler())
            .SetErrorStatusOnException()
            .AddConsoleExporter()
            .Build();

        try
        {
            using (MyActivitySource.StartActivity("Foo"))
            {
                using (MyActivitySource.StartActivity("Bar"))
                {
                    throw new Exception("Oops!");
                }
            }
        }
        catch (Exception)
        {
            // swallow the exception
        }
    }
}
