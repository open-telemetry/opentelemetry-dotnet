// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using Greet;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Instrumentation.Grpc.Services.Tests;

public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger logger;

    public GreeterService(ILoggerFactory loggerFactory)
    {
        this.logger = loggerFactory.CreateLogger<GreeterService>();
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        this.logger.LogInformation("Sending hello to {Name}", request.Name);
        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }

    public override async Task SayHellos(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        var i = 0;
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var message = $"How are you {request.Name}? {++i}";
            this.logger.LogInformation("Sending greeting {Message}.", message);

            await responseStream.WriteAsync(new HelloReply { Message = message }).ConfigureAwait(false);

            // Gotta look busy
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }
}
