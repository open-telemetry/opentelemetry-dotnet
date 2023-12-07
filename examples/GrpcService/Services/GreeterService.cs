// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;

namespace Examples.GrpcService;

public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> logger;

    public GreeterService(ILogger<GreeterService> logger)
    {
        this.logger = logger;
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name,
        });
    }
}