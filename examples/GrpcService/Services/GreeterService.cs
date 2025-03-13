// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;

namespace Examples.GrpcService;

internal class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name,
        });
    }
}
