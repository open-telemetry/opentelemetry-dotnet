#!/bin/bash
set -e

if [ ! -f /cfg/certs/otel-test-ca-cert.pem ]; then
  echo "CA certificate not found!"
  exit 1
fi

# Trust the self-signed certificate used by the collector
cp /cfg/certs/otel-test-ca-cert.pem /usr/local/share/ca-certificates/otel-test-ca-cert.crt
update-ca-certificates --verbose

sleep 100000

dotnet test OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.dll --TestCaseFilter:CategoryName=CollectorIntegrationTests --logger "console;verbosity=detailed"
