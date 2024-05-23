// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Tests.Stress;

public static class Program
{
    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<LogsStressTest>(args);
    }

    private sealed class LogsStressTest : StressTest<StressTestOptions>
    {
        private static readonly Payload Payload = new();
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        public LogsStressTest(StressTestOptions options)
            : base(options)
        {
            this.loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.UseOpenTelemetry(logging =>
                {
                    logging.AddProcessor(new DummyProcessor());
                });
            });

            this.logger = this.loggerFactory.CreateLogger<LogsStressTest>();
        }

        protected override void RunWorkItemInParallel()
        {
            this.logger.Log(
                logLevel: LogLevel.Information,
                eventId: 2,
                state: Payload,
                exception: null,
                formatter: (state, ex) => string.Empty);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.loggerFactory.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
