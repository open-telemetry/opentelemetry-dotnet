// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Prometheus;

public static class PrometheusAcceptHeaders
{
    public static TheoryData<string, string, bool, string, string?> Valid()
    {
        var testCases = new TheoryData<string, string, bool, string, string?>();

        string[] prometheusV0 =
        [
            "text/plain",
            "text/plain; charset=utf-8",
            "text/plain; charset=utf-8; version=0.0.4",
            "text/plain,application/openmetrics-text; version=1.0.0; charset=utf-8",
            "text/plain, application/openmetrics-text; version=1.0.0; charset=utf-8",
            "text/plain, application/openmetrics-text; version=1.0.0; charset=utf-8",
            "text/plain; charset=utf-8,application/openmetrics-text; version=1.0.0; charset=utf-8",
            "text/plain, */*;q=0.8,application/openmetrics-text; version=1.0.0; charset=utf-8",
            "text/plain;version=0.0.4;q=0.6,*/*;q=0.5",
            "text/plain; q=0.9, application/openmetrics-text; version=1.0.0; q=0.1",
            "TEXT/PLAIN; q=0.9, Application/OpenMetrics-Text; version=1.0.0; q=0.1",
            "*/*;q=0.8,text/plain; charset=utf-8; version=0.0.4",
        ];

        foreach (var accept in prometheusV0)
        {
            testCases.Add(accept, "text/plain", false, "0.0.4", null);
        }

        string[] prometheusV1 =
        [
            "text/plain;version=1.0.0;escaping=allow-utf-8;q=0.6,*/*;q=0.5",
        ];

        foreach (var accept in prometheusV1)
        {
            testCases.Add(accept, "text/plain", false, "1.0.0", "underscores");
        }

        string[] openMetricsV0 =
        [
            "application/openmetrics-text",
            "application/openmetrics-text;version=0.0.1;q=0.6,*/*;q=0.5",
            "application/openmetrics-text; version=0.0.1",
            "application/openmetrics-text; version=0.0.1; charset=utf-8",
            "application/openmetrics-text; version=\"0.0.1\"",
        ];

        foreach (var accept in openMetricsV0)
        {
            testCases.Add(accept, "application/openmetrics-text", true, "0.0.1", null);
        }

        string[] openMetricsV1 =
        [
            "application/openmetrics-text; version=1.0.0",
            "application/openmetrics-text; version=\"1.0.0\"",
            "application/openmetrics-text; version=1.0.0; escaping=allow-utf-8",
            "application/openmetrics-text; version=1.0.0; escaping=underscores",
            "application/openmetrics-text; version=\"1.0.0\"; escaping=\"underscores\"",
            "application/openmetrics-text; version=1.0.0; charset=utf-8",
            "Application/OpenMetrics-Text; version=1.0.0",
            "application/openmetrics-text; version=1.0.0; charset=utf-8; escaping=underscores",
            "application/openmetrics-text; version=1.0.0; charset=utf-8; escaping=allow-utf-8",
            "text/plain; q=0.3, application/openmetrics-text; version=1.0.0; q=0.9",
            "TEXT/PLAIN; q=0.3, Application/OpenMetrics-Text; version=1.0.0; q=0.9",
            "application/openmetrics-text;version=1.0.0;escaping=allow-utf-8;q=0.6,*/*;q=0.5",
            "application/openmetrics-text;version=1.0.0;escaping=allow-utf-8;q=0.6,application/openmetrics-text;version=0.0.1;q=0.5,text/plain;version=1.0.0;escaping=allow-utf-8;q=0.4,text/plain;version=0.0.4;q=0.3,*/*;q=0.2",
            "application/openmetrics-text;version=1.0.0;escaping=underscores;q=0.6,*/*;q=0.5",
            "application/openmetrics-text;version=1.0.0;escaping=underscores;q=0.6,application/openmetrics-text;version=0.0.1;q=0.5,text/plain;version=1.0.0;escaping=allow-utf-8;q=0.4,text/plain;version=0.0.4;q=0.3,*/*;q=0.2",
        ];

        foreach (var accept in openMetricsV1)
        {
            testCases.Add(accept, "application/openmetrics-text", true, "1.0.0", "underscores");
        }

        return testCases;
    }

#pragma warning disable CA1825 // Avoid zero-length array allocations
    public static TheoryData<string> Invalid() =>
    [
        string.Empty,
        "foo",
        "application/json",
        "application/openmetrics-text; version=0.0.5",
        "application/openmetrics-text; version=foo",
        "application/openmetrics-text; version=1.0.0; q=0",
        "application/openmetrics-text; version=1.0.0; escaping=dots",
        "application/openmetrics-text; version=1.0.0; escaping=foo",
        "application/openmetrics-text; version=1.0.0; escaping=values",
        "application/openmetrics-text; version=2.0.0",
        "text/plain; q=0, application/openmetrics-text; version=1.0.0; q=0",
    ];
#pragma warning restore CA1825 // Avoid zero-length array allocations
}
