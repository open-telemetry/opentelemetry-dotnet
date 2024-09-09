# ca
openssl req -new -newkey rsa:2048 -days 365 -nodes -x509 \
    -subj "/CN=otel-test-ca" \
    -keyout $1/otel-test-ca-key.pem  -out $1/otel-test-ca-cert.pem

# server cert
echo "\
basicConstraints = CA:FALSE
nsCertType = server
nsComment = "OpenSSL Generated Server Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer:always
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
" > $1/server_cert_ext.cnf;

openssl req -new -newkey rsa:2048 -sha256 \
    -keyout $1/otel-test-server-key.pem -out $1/otel-test-server-csr.pem -nodes \
    -subj "/CN=otel-test-server"

openssl x509 -req -in $1/otel-test-server-csr.pem \
    -extfile $1/server_cert_ext.cnf \
    -CA $1/otel-test-ca-cert.pem -CAkey $1/otel-test-ca-key.pem -CAcreateserial \
    -out $1/otel-test-server-cert.pem \
    -days 3650 -sha256

# client cert
echo "\
basicConstraints = CA:FALSE
nsCertType = client, email
nsComment = "OpenSSL Generated Client Certificate"
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = clientAuth, emailProtection
" > $1/client_cert_ext.cnf;

openssl req -new -newkey rsa:2048 -sha256 \
    -keyout $1/otel-test-client-key.pem -out $1/otel-test-client-csr.pem -nodes \
    -subj "/CN=otel-test-client"

openssl x509 -req -in $1/otel-test-client-csr.pem \
    -extfile $1/client_cert_ext.cnf \
    -CA $1/otel-test-server-cert.pem -CAkey $1/otel-test-server-key.pem -CAcreateserial \
    -out $1/otel-test-client-cert.pem \
    -days 3650 -sha256