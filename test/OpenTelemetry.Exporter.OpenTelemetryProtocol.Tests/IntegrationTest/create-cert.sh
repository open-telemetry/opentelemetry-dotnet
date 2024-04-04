#!/bin/bash
set -e

# Generate self-signed certificate for the collector
openssl req -new -newkey rsa:2048 -days 365 -nodes -x509 \
    -subj "/CN=otel-collector" \
    -keyout /otel-collector.key  -out /otel-collector.crt

# Copy the certificate and private key file to shared volume that the collector
# container and test container can access
cp /otel-collector.crt /otel-collector.key /cfg

chmod 644 /cfg/otel-collector.key

# The integration test is run via docker-compose with the --exit-code-from
# option. The --exit-code-from option implies --abort-on-container-exit
# which means when any container exits then all containers are stopped.
# Since the container running this script would be otherwise short-lived
# we sleep here. If the test does not finish within this time then the test
# container will be stopped and have a non-zero exit code.
sleep 300
