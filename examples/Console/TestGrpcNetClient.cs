// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Examples.GrpcService;
using Grpc.Core;
using Grpc.Net.Client;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal class TestGrpcNetClient
{
    internal static int Run(GrpcNetClientOptions options)
    {
        // Prerequisite for running this example.
        // In a separate console window, start the example
        // ASP.NET Core gRPC service by running the following command
        // from the reporoot\examples\GrpcService\.
        // (eg: C:\repos\opentelemetry-dotnet\examples\GrpcService\)
        //
        // dotnet run

        // To run this example, run the following command from
        // the reporoot\examples\Console\.
        // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
        //
        // dotnet run grpc

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddGrpcClientInstrumentation()
            .AddSource("grpc-net-client-test")
            .AddConsoleExporter()
            .Build();

        using var source = new ActivitySource("grpc-net-client-test");
        using (var parent = source.StartActivity("Main", ActivityKind.Server))
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:44335");
            var client = new Greeter.GreeterClient(channel);

            try
            {
                var reply = client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" }).GetAwaiter().GetResult();
                System.Console.WriteLine($"Message received: {reply.Message}");
            }
            catch (RpcException)
            {
                System.Console.Error.WriteLine($"To run this Grpc.Net.Client example, first start the Examples.GrpcService project.");
                throw;
            }
        }

        System.Console.WriteLine("Press Enter key to exit.");
        System.Console.ReadLine();

        return 0;
    }
}
