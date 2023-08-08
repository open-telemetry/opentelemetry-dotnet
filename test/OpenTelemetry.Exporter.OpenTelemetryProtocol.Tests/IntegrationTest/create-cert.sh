#!/bin/bash

# Generate self-signed certificate for the collector
openssl req -new -newkey rsa:2048 -days 365 -nodes -x509 \
    -subj "/CN=otel-collector" \
    -keyout /otel-collector.key  -out /otel-collector.crt

# Copy the certificate and private key file to shared volume that the collector
# container and test container can access
cp /otel-collector.crt /otel-collector.key /cfg

chmod 644 /cfg/otel-collector.key

# Generate CA and client cert for mTLS
echo "\
basicConstraints = CA:FALSE
nsCertType = server
nsComment = "OpenSSL Generated CA Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer:always
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
" > /ca_cert_ext.cnf

openssl ecparam -genkey -name prime256v1 -out /otel-ca-key.pem
openssl req -new -sha256 -key /otel-ca-key.pem -out /otel-ca-csr.pem -subj "/CN=otel-test-ca"
openssl x509 -req -in /otel-ca-csr.pem -sha256 -days 365 -signkey /otel-ca-key.pem -out /otel-ca-cert.pem -extfile /ca_cert_ext.cnf

echo "\
basicConstraints = CA:FALSE
nsCertType = client, email
nsComment = "OpenSSL Generated Client Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = clientAuth, emailProtection
" > client_cert_ext.cnf

openssl ecparam -genkey -name prime256v1 -out /otel-client-key.pem
openssl req -new -key /otel-client-key.pem -out /otel-client-csr.pem -subj "/CN=otel-test-client"
openssl x509 -req -in /otel-client-csr.pem -CA /otel-ca-cert.pem -CAkey /otel-ca-key.pem -out /otel-client-cert.pem -CAcreateserial -days 365 -sha256 -extfile /client_cert_ext.cnf

cp /otel-ca-cert.pem /otel-client-cert.pem /otel-client-key.pem /cfg
cp /otel-ca-cert.pem /usr/local/share/ca-certificates/otel-ca-cert.pem

# The integration test is run via docker-compose with the --exit-code-from
# option. The --exit-code-from option implies --abort-on-container-exit
# which means when any container exits then all containers are stopped.
# Since the container running this script would be otherwise short-lived
# we sleep here. If the test does not finish within this time then the test
# container will be stopped and have a non-zero exit code.
sleep 300
