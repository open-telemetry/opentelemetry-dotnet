#!/bin/bash

# Set output directory, default is the current directory
OUT_DIR=${1:-"."}

# Create output directory if it doesn't exist
mkdir -p "$OUT_DIR"

# Generate CA certificate (Certificate Authority)
openssl req -new -newkey rsa:2048 -days 3650 -nodes -x509 \
    -subj "/CN=otel-test-ca" \
    -keyout "$OUT_DIR/otel-test-ca-key.pem" -out "$OUT_DIR/otel-test-ca-cert.pem"

# Create the extension configuration file for the server certificate
cat > "$OUT_DIR/server_cert_ext.cnf" <<EOF
[ v3_ca ]
basicConstraints = CA:FALSE
nsCertType = server
nsComment = "OpenSSL Generated Server Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer:always
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[ alt_names ]
DNS.1 = otel-collector
EOF

# Generate server certificate private key and CSR (Certificate Signing Request)
openssl req -new -newkey rsa:2048 -sha256 -nodes \
    -keyout "$OUT_DIR/otel-test-server-key.pem" -out "$OUT_DIR/otel-test-server-csr.pem" \
    -subj "/CN=otel-collector"

# Sign the server certificate using the CA certificate
openssl x509 -req -in "$OUT_DIR/otel-test-server-csr.pem" \
    -extfile "$OUT_DIR/server_cert_ext.cnf" -extensions v3_ca \
    -CA "$OUT_DIR/otel-test-ca-cert.pem" -CAkey "$OUT_DIR/otel-test-ca-key.pem" -CAcreateserial \
    -out "$OUT_DIR/otel-test-server-cert.pem" \
    -days 3650 -sha256

# Create the extension configuration file for the client certificate
cat > "$OUT_DIR/client_cert_ext.cnf" <<EOF
[ v3_client ]
basicConstraints = CA:FALSE
nsCertType = client, email
nsComment = "OpenSSL Generated Client Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = clientAuth, emailProtection
EOF

# Generate client certificate private key and CSR
openssl req -new -newkey rsa:2048 -sha256 -nodes \
    -keyout "$OUT_DIR/otel-test-client-key.pem" -out "$OUT_DIR/otel-test-client-csr.pem" \
    -subj "/CN=otel-test-client"

# Sign the client certificate using the CA certificate
openssl x509 -req -in "$OUT_DIR/otel-test-client-csr.pem" \
    -extfile "$OUT_DIR/client_cert_ext.cnf" -extensions v3_client \
    -CA "$OUT_DIR/otel-test-ca-cert.pem" -CAkey "$OUT_DIR/otel-test-ca-key.pem" -CAcreateserial \
    -out "$OUT_DIR/otel-test-client-cert.pem" \
    -days 3650 -sha256

# Generate an untrusted self-signed certificate (not signed by the CA)
openssl req -new -newkey rsa:2048 -days 365 -nodes -x509 \
    -subj "/CN=otel-untrusted-collector" \
    -keyout "$OUT_DIR/otel-untrusted-collector-key.pem" -out "$OUT_DIR/otel-untrusted-collector-cert.pem"
