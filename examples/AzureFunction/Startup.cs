// <copyright file="Startup.cs" company="OpenTelemetry Authors">
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

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

[assembly: FunctionsStartup(typeof(Examples.AzureFunction.Startup))]

namespace Examples.AzureFunction
{
    public class Startup : FunctionsStartup
    {
        private static TracerProvider tracerProvider;

        public override void Configure(IFunctionsHostBuilder builder)
        {
            // TODO: It does not appear that the OpenTelemetry.Extensions.Hosting package is compatible with Azure Functions.
            // Using it causes the following error:
            //     [Invalid] ServiceType: Microsoft.Extensions.Hosting.IHostedService, Lifetime: Singleton, ImplementationType: OpenTelemetry.Extensions.Hosting.Implementation.TelemetryHostedService, OpenTelemetry.Extensions.Hosting, Version = 0.2.0.909, Culture = neutral, PublicKeyToken = 7bd6737fe5b67e3c.
            // builder.Services.AddOpenTelemetryTracing(builder =>
            // {
            //     builder
            //         .AddSource("MyFunction")
            //         .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyFunction"))
            //         .AddHttpClientInstrumentation()
            //         .AddConsoleExporter();
            // });

            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("MyFunction")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyFunction"))
                .AddHttpClientInstrumentation()
                .AddConsoleExporter()
                .Build();
        }
    }
}
