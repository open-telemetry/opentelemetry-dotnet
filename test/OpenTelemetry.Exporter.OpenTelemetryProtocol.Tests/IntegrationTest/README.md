# OTLP exporter integration tests

This directory contains a suite of integration tests that can be run using
docker compose.

Run the following command from the root of the repository to run the
integration tests locally:

```shell
docker compose \
    --file=test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/docker-compose.yml \
    --project-directory=. \
    up \
    --exit-code-from=tests \
    --build
```
