// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;

namespace Examples.GrpcService;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class GreeterService : Greeter.GreeterBase
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name,
        });
    }
}
