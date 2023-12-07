// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Benchmarks.Logs;

public static partial class Food
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Hello from {food} {price}.")]
    public static partial void SayHello(
        ILogger logger, string food, double price);
}
