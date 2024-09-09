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

# Generate client certificate for mTLS
echo "\
basicConstraints = CA:FALSE
nsCertType = client, email
nsComment = "OpenSSL Generated Client Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = clientAuth, emailProtection
" > /client_ext.cnf

openssl req -new -newkey rsa:2048 -days 365 -nodes \
    -subj "/CN=otel-client" \
    -keyout /otel-client.key  -out /otel-client.csr

openssl x509 -req -in /otel-client.csr \
    -CA /otel-collector.crt -CAkey /otel-collector.key \
    -out /otel-client.crt -CAcreateserial -days 365 -sha256 \
    -extfile ./client_ext.cnf

cp /otel-client.crt /otel-client.key /cfg
chmod 644 /cfg/otel-client.key

# Generate a self-signed certificate that is NOT included in the test runner's trust store
# Generate self-signed certificate for the Collector
openssl req -new -newkey rsa:2048 -days 365 -nodes -x509 \
    -subj "/CN=otel-collector" \
    -keyout /otel-untrusted-collector.key  -out /otel-untrusted-collector.crt

cp /otel-untrusted-collector.crt /otel-untrusted-collector.key /cfg
chmod 644 /cfg/otel-untrusted-collector.key

# The integration test is run via docker-compose with the --exit-code-from
# option. The --exit-code-from option implies --abort-on-container-exit
# which means when any container exits then all containers are stopped.
# Since the container running this script would be otherwise short-lived
# we sleep here. If the test does not finish within this time then the test
# container will be stopped and have a non-zero exit code.
sleep 300
