// <copyright file="TestGrpcNetClient.cs" company="OpenTelemetry Authors">
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
using Examples.GrpcService;
using Grpc.Core;
using Grpc.Net.Client;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestGrpcNetClient
    {
        internal static object Run()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddGrpcClientInstrumentation()
                .AddSource("grpc-net-client-test")
                .AddConsoleExporter()
                .Build();

            var source = new ActivitySource("grpc-net-client-test");
            using (var parent = source.StartActivity("Main", ActivityKind.Server))
            {
                using var channel = GrpcChannel.ForAddress("https://localhost:5001");
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

            return null;
        }
    }
}
