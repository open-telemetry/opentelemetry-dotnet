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

using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace ReportingExceptions;

public class Program
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
