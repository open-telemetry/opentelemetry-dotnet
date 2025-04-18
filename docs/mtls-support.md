# mTLS Support in OpenTelemetry .NET OTLP Exporter

The OpenTelemetry .NET OTLP exporter supports mutual TLS (mTLS) for secure communications **ONLY in .NET 8.0 or later**. This document explains how to configure and use mTLS with the OTLP exporter.

## Prerequisites

- **.NET 8.0 or later** (there is no support for earlier versions)
- PEM-formatted certificates and keys

## Features

- Support for trusted server certificates (CA certificates)
- Support for client certificates for mutual authentication
- Certificate chain validation
- File permission security checks
- Graceful fallback if certificate loading fails

## Configuration Options

mTLS can be configured in two ways:

### 1. Using Environment Variables

```bash
# Path to trusted CA certificate for server verification
export OTEL_EXPORTER_OTLP_CERTIFICATE=/path/to/ca.pem

# Path to client certificate for client authentication
export OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE=/path/to/client.pem

# Path to client private key for client authentication
export OTEL_EXPORTER_OTLP_CLIENT_KEY=/path/to/client-key.pem
```

### 2. Using Code Configuration

```csharp
var options = new OtlpExporterOptions
{
    // Path to trusted CA certificate for server verification
    CertificateFilePath = "/path/to/ca.pem",

    // Path to client certificate for client authentication
    ClientCertificateFilePath = "/path/to/client.pem",

    // Path to client private key for client authentication
    ClientKeyFilePath = "/path/to/client-key.pem"
};

// Configure the exporter with these options
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddOtlpExporter(opt =>
        {
            opt.CertificateFilePath = options.CertificateFilePath;
            opt.ClientCertificateFilePath = options.ClientCertificateFilePath;
            opt.ClientKeyFilePath = options.ClientKeyFilePath;
        }));
```

## Certificate Requirements

All certificates must be in PEM format.

### CA Certificate

The CA certificate is used to verify the server's certificate. It should be a trusted certificate that has signed the server's certificate.

Example:
```
-----BEGIN CERTIFICATE-----
MIIBxTCCAWugAwIBAgIJAM06Hx4442JaMA0GCSqGSIb3DQEBCwUAMCMxITAfBgNV
...
FCiqwDQ+Gd3xCf8xna9ZS/9EqEVR3roW
-----END CERTIFICATE-----
```

### Client Certificate and Key

The client certificate and key are used to authenticate the client to the server. The client certificate must be signed by a CA that the server trusts.

Client Certificate Example:
```
-----BEGIN CERTIFICATE-----
MIIBxTCCAWugAwIBAgIJAM06Hx4442JaMA0GCSqGSIb3DQEBCwUAMCMxITAfBgNV
...
FCiqwDQ+Gd3xCf8xna9ZS/9EqEVR3roW
-----END CERTIFICATE-----
```

Client Key Example:
```
-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7VJTUt9Us8cKj
...
Mq+aI9o9+AdS+FYvpWxnwZHGpfiD9rL1JXw=
-----END PRIVATE KEY-----
```

## Security Considerations

- **File Permissions**: On Unix-like systems (Linux/macOS), certificate and key files should have restrictive permissions (chmod 400 or 600). On Windows, ACLs should be configured to restrict access to the service account only.
- **Certificate Chain Validation**: All certificates are validated to ensure their integrity and authenticity.
- **Error Handling**: If certificate loading or validation fails, appropriate errors are logged, and the system gracefully falls back to non-mTLS communication if possible.

## Logging

The mTLS implementation logs all relevant events through the OpenTelemetry event source:

- Certificate loading errors
- Chain validation failures
- Permission issues
- Successful configuration

## Compatibility

- **Supported ONLY in .NET 8.0+**: The mTLS features are exclusively available when running on .NET 8.0 or later. In earlier versions, these settings will be completely ignored, and attempting to use them will result in a `PlatformNotSupportedException`.
- **Protocol Compatibility**: Both HTTP and gRPC protocols are supported with mTLS.

## Testing Your Configuration

A simple way to test your mTLS configuration is to use the following code:

```csharp
// Note: This code requires .NET 8.0 or later
var options = new OtlpExporterOptions
{
    Endpoint = new Uri("https://your-collector:4317"),
    CertificateFilePath = "/path/to/ca.pem",
    ClientCertificateFilePath = "/path/to/client.pem",
    ClientKeyFilePath = "/path/to/client-key.pem"
};

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddOtlpExporter(opt =>
    {
        opt.CertificateFilePath = options.CertificateFilePath;
        opt.ClientCertificateFilePath = options.ClientCertificateFilePath;
        opt.ClientKeyFilePath = options.ClientKeyFilePath;
        opt.Endpoint = options.Endpoint;
    })
    .Build();

// Create a tracer and send some spans
var tracer = tracerProvider.GetTracer("TestTracer");
using (var span = tracer.StartActiveSpan("TestSpan"))
{
    // Do something
    Console.WriteLine("Span sent with mTLS");
}
```

## Troubleshooting

If you encounter issues with mTLS:

1. Check file permissions on certificate files
2. Verify that the CA certificate is trusted by the server
3. Check that the client certificate is valid and not expired
4. Ensure the private key matches the client certificate
5. Look for error logs in your application's log output
6. Try enabling more verbose logging for OpenTelemetry
7. Verify you're running on .NET 8.0 or later, as mTLS is not supported on earlier versions

For more detailed troubleshooting, enable debug logging on your application to see detailed error messages from the OpenTelemetry exporter.
