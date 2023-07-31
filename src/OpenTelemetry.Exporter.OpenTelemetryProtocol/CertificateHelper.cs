// <copyright file="CertificateHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Security.Cryptography.X509Certificates;

#if !NET5_0_OR_GREATER
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO.Pem;
using Org.BouncyCastle.X509;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol;

internal static class CertificateHelper
{
    public static X509Certificate2 LoadCertificateFromPemFile(string certificatePath, string privateKeyPath)
    {
#if NET5_0_OR_GREATER
        // API here is a bit tricky, CreateFromPemFile requires private key,
        // while CreateFromPem can simply load the public cert
        using var pemCert = privateKeyPath is null
            ? X509Certificate2.CreateFromPem(File.ReadAllText(certificatePath))
            : X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);

        // loading ephemeral pem files have problem on Windows
        // https://github.com/dotnet/runtime/issues/23749
        return new X509Certificate2(pemCert.Export(X509ContentType.Pkcs12));
#else
        if (privateKeyPath == null)
        {
            return LoadFromPemWithoutKey(certificatePath);
        }

        var certContent = File.ReadAllBytes(certificatePath);
        var certParser = new X509CertificateParser();
        var cert = certParser.ReadCertificate(certContent);

        var privateKeyPem = LoadPemFromFile(privateKeyPath);

        AsymmetricKeyParameter privateKeyParams = privateKeyPem.Type switch
        {
            "RSA PRIVATE KEY" => new RsaPrivateCrtKeyParameters(RsaPrivateKeyStructure.GetInstance(privateKeyPem.Content)),
            _ => throw new ArgumentOutOfRangeException(nameof(privateKeyPath), $"Cannot load private key of type '{privateKeyPem.Type}'"),
        };

        // .NET Framework can only load certificate and key from pkcs12
        // so the easiest way is to convert to that format
        Pkcs12Store store = new Pkcs12StoreBuilder().Build();
        store.SetKeyEntry("private_key", new AsymmetricKeyEntry(privateKeyParams), new X509CertificateEntry[] { new X509CertificateEntry(cert) });

        using (var pkcs12Stream = new MemoryStream())
        {
            store.Save(pkcs12Stream, Array.Empty<char>(), SecureRandom.GetInstance("SHA256PRNG"));
            var certData = pkcs12Stream.ToArray();

            return new X509Certificate2(certData);
        }
#endif
    }

#if !NET5_0_OR_GREATER
    private static PemObject LoadPemFromFile(string path)
    {
        using (var fileStream = File.OpenRead(path))
        using (var textReader = new StreamReader(fileStream))
        using (var pemReader = new PemReader(textReader))
        {
            return pemReader.ReadPemObject();
        }
    }

    private static X509Certificate2 LoadFromPemWithoutKey(string path)
    {
        using (var fileStream = File.OpenRead(path))
        using (var textReader = new StreamReader(fileStream))
        using (var pemReader = new PemReader(textReader))
        {
            var pemObject = pemReader.ReadPemObject();
            var certificate = new X509Certificate2();
            certificate.Import(pemObject.Content);
            return certificate;
        }
    }
#endif
}
