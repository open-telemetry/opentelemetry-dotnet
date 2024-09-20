#!/bin/bash
set -e

if [ ! -f /cfg/certs/otel-test-ca-cert.pem ]; then
  echo "CA certificate not found!"
  exit 1
fi

# Trust the self-signed certificated used by the collector
cp /cfg/certs/otel-test-ca-cert.pem /usr/local/share/ca-certificates/
update-ca-certificates --verbose

dotnet test OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.dll --TestCaseFilter:CategoryName=CollectorIntegrationTests --logger "console;verbosity=detailed"
