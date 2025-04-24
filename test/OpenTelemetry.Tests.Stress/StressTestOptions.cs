// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace OpenTelemetry.Tests.Stress;

#pragma warning disable CA1515 // Consider making public types internal
public class StressTestOptions
#pragma warning restore CA1515 // Consider making public types internal
{
    [Option('c', "concurrency", HelpText = "The concurrency (maximum degree of parallelism) for the stress test. Default value: Environment.ProcessorCount.", Required = false)]
    public int Concurrency { get; set; }

    [Option('p', "internal_port", HelpText = "The Prometheus http listener port where Prometheus will be exposed for retrieving internal metrics while the stress test is running. Set to '0' to disable. Default value: 9464.", Required = false)]
    public int PrometheusInternalMetricsPort { get; set; } = 9464;

    [Option('d', "duration", HelpText = "The duration for the stress test to run in seconds. If set to '0' or a negative value the stress test will run until canceled. Default value: 0.", Required = false)]
    public int DurationSeconds { get; set; }
}
