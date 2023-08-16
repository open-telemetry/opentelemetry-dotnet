// <copyright file="Meat.cs" company="OpenTelemetry Authors">
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

using System.Runtime.CompilerServices;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Proto.Collector.Logs.V1;
using SourceGeneration;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static ILogger? logger;
    private static ILoggerFactory? loggerFactory;

    public static void Main()
    {
        var host = new HostBuilder()
           .ConfigureWebHostDefaults(webBuilder => webBuilder
                .ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(4317, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
                })
               .ConfigureServices(services =>
               {
                   services.AddGrpc();
               })
               .Configure(app =>
               {
                   app.UseRouting();

                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapGrpcService<MockLogService>();
                   });
               }))
           .Start();

        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.IncludeFormattedMessage = true;
                options.AddOtlpExporter();
            });
        });

        logger = loggerFactory?.CreateLogger<Program>();

        Run();

        // Stress(concurrency: 1, prometheusPort: 9464);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        logger?.FoodRecallNotice(
           logLevel: LogLevel.Critical,
           brandName: "Contoso",
           productDescription: "Salads",
           productType: "Food & Beverages",
           recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
           companyName: "Contoso Fresh Vegetables, Inc.");
    }

    private class MockLogService : LogsService.LogsServiceBase
    {
        private static ExportLogsServiceResponse response = new ExportLogsServiceResponse();

        public override Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(response);
        }
    }
}
