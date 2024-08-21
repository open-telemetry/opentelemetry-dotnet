// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

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
                builder.AddOpenTelemetry(logging =>
                {
                    logging.AddProcessor(new DummyProcessor());
                });
            });

            this.logger = this.loggerFactory.CreateLogger<LogsStressTest>();
        }

        protected override void RunWorkItemInParallel()
        {
            this.logger.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
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
