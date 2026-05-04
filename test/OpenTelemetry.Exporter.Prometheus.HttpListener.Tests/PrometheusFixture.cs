// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusFixture : XunitContainerFixture<IContainer>
{
    public IList<string>? ScrapeProtocols { get; set; }

    public int? TargetPort { get; set; }

    protected override string DockerfileName => "prometheus.Dockerfile";

    protected override IContainer CreateContainer()
    {
        if (this.TargetPort is not { } targetPort)
        {
            throw new InvalidOperationException($"No scrape target port configured.");
        }

        var prometheusConfigurationPath = Path.GetTempFileName();
        File.WriteAllText(prometheusConfigurationPath, CreatePrometheusConfiguration(ScrapeProtocols));

        var sdPath = Path.GetTempFileName();
        File.WriteAllText(sdPath, CreateServiceDiscoveryConfiguration(targetPort));

        return new ContainerBuilder(this.GetImage())
            .WithBindMount(prometheusConfigurationPath, "/etc/prometheus/prometheus.yml")
            .WithBindMount(sdPath, "/etc/prometheus/targets/targets.json")
            .WithCommand("--config.file=/etc/prometheus/prometheus.yml")
            .WithPortBinding(4318)
            .WithPortBinding(9090)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(4318))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(9090))
            .Build();
    }

    private static string CreatePrometheusConfiguration(IList<string>? scrapeProtocols) =>
        $"""
         global:
           scrape_interval: 2s
           {(scrapeProtocols is not null ? $"scrape_protocols: [\"{string.Join("\", \"", scrapeProtocols)}\"]" : string.Empty)}
         scrape_configs:
           - job_name: "prometheus-target"
             file_sd_configs:
               - files:
                   - /etc/prometheus/targets/targets.json
                 refresh_interval: 1s
         """;

    private static string CreateServiceDiscoveryConfiguration(int port) =>
        $$"""
          [
            {
              "labels": { "job": "prometheus-target" },
              "targets": ["host.docker.internal:{{port}}"]
            }
          ]
          """;
}
