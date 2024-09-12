using namespace System.Security.Cryptography;
using namespace System.Security.Cryptography.X509Certificates;

param (
  [string] $OutDir
)

function Write-Certificate {
  param (
    [X509Certificate2] $Cert,
    [string] $Name,
    [string] $Dir
  )
  
  # write cert content
  $certPem = $Cert.ExportCertificatePem();
  $certPemPath = Join-Path $Dir -ChildPath "$Name-cert.pem";
  [System.IO.File]::WriteAllText($certPemPath, $certPem);

  # write pkey
  [AsymmetricAlgorithm] $pkey = [RSACertificateExtensions]::GetRSAPrivateKey($Cert);
  [string] $pkeyPem = $null;

  if ($null -ne $pkey) {
    $pkeyPem = $pkey.ExportRSAPrivateKeyPem();
  }

  if ($null -eq $pkey) {
    $pkey = [ECDsaCertificateExtensions]::GetECDsaPrivateKey($Cert);
    $pkeyPem = $pkey.ExportECPrivateKeyPem();
  }

  if ($null -eq $pkeyPem) {
    return;
  }

  
  $pKeyPath = Join-Path $Dir -ChildPath "$Name-key.pem";
  [System.IO.File]::WriteAllText($pKeyPath, $pkeyPem);
}

# Generate CA certificate (Certificate Authority)
$ca = New-SelfSignedCertificate -CertStoreLocation 'Cert:\CurrentUser\My' `
  -DnsName "otel-test-ca" `
  -NotAfter (Get-Date).AddYears(20) `
  -FriendlyName "otel-test-ca" `
  -KeyAlgorithm ECDSA_nistP256 `
  -KeyExportPolicy Exportable `
  -KeyUsageProperty All -KeyUsage CertSign, CRLSign, DigitalSignature;

  
try {
  # Write the CA cert
  Write-Certificate -Cert $ca -Name "otel-test-ca" -Dir $OutDir;

  # Generate server certificate (otel-test-server)
  $serverCert = New-SelfSignedCertificate -CertStoreLocation 'Cert:\CurrentUser\My' `
    -DnsName "otel-test-server" `
    -Signer $ca `
    -NotAfter (Get-Date).AddYears(20) `
    -FriendlyName "otel-test-server" `
    -KeyAlgorithm ECDSA_nistP256 `
    -KeyUsageProperty All `
    -KeyExportPolicy Exportable `
    -KeyUsage CertSign, CRLSign, DigitalSignature `
    -TextExtension @("2.5.29.19={text}CA=1&pathlength=1", "2.5.29.37={text}1.3.6.1.5.5.7.3.1");

  try {
    # Write the server cert
    Write-Certificate -Cert $serverCert -Name "otel-test-server" -Dir $OutDir;

    # Generate client certificate (otel-test-client) for mTLS
    $clientCert = New-SelfSignedCertificate -CertStoreLocation 'Cert:\CurrentUser\My' `
      -DnsName "otel-test-client" `
      -Signer $serverCert `
      -NotAfter (Get-Date).AddYears(20) `
      -FriendlyName "otel-test-client" `
      -KeyAlgorithm ECDSA_nistP256 `
      -KeyUsageProperty All `
      -KeyExportPolicy Exportable `
      -KeyUsage CertSign, CRLSign, DigitalSignature `
      -TextExtension @("2.5.29.19={text}CA=1&pathlength=1", "2.5.29.37={text}1.3.6.1.5.5.7.3.2");

    try {
      # Write the client cert
      Write-Certificate -Cert $clientCert -Name "otel-test-client" -Dir $OutDir;
    }
    finally {
      Get-Item -Path "Cert:\CurrentUser\My\$($clientCert.Thumbprint)" | Remove-Item;
    }

    # Generate untrusted collector certificate
    $untrustedCollectorCert = New-SelfSignedCertificate -CertStoreLocation 'Cert:\CurrentUser\My' `
      -DnsName "otel-untrusted-collector" `
      -NotAfter (Get-Date).AddYears(1) `
      -FriendlyName "otel-untrusted-collector" `
      -KeyAlgorithm ECDSA_nistP256 `
      -KeyUsageProperty All `
      -KeyExportPolicy Exportable `
      -KeyUsage CertSign, CRLSign, DigitalSignature `
      -TextExtension @("2.5.29.19={text}CA=0", "2.5.29.37={text}1.3.6.1.5.5.7.3.1");

    try {
      # Write the untrusted collector cert
      Write-Certificate -Cert $untrustedCollectorCert -Name "otel-untrusted-collector" -Dir $OutDir;
    }
    finally {
      Get-Item -Path "Cert:\CurrentUser\My\$($untrustedCollectorCert.Thumbprint)" | Remove-Item;
    }

  }
  finally {
    Get-Item -Path "Cert:\CurrentUser\My\$($serverCert.Thumbprint)" | Remove-Item;
  }

}
finally {
  Get-Item -Path "Cert:\CurrentUser\My\$($ca.Thumbprint)" | Remove-Item;
}
