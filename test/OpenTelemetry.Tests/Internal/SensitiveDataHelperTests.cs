// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal.Tests;

public class SensitiveDataHelperTests
{
    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----\nTEST-PRIVATE-KEY\n-----END PRIVATE KEY-----", true)]
    [InlineData("-----BEGIN CERTIFICATE----- TEST-CERTIFICATE -----END CERTIFICATE-----", true)]
    [InlineData("  -----BEGIN RSA PRIVATE KEY----- TEST-PRIVATE-KEY -----END RSA PRIVATE KEY-----", true)]
    [InlineData("\"-----BEGIN PRIVATE KEY-----\\nTEST-PRIVATE-KEY\\n-----END PRIVATE KEY-----\"", true)]
    [InlineData("'-----BEGIN PRIVATE KEY-----\\nTEST-PRIVATE-KEY\\n-----END PRIVATE KEY-----'", true)]
    [InlineData("prefix:-----BEGIN PRIVATE KEY-----\\nTEST-PRIVATE-KEY\\n-----END PRIVATE KEY-----", true)]
    [InlineData("LS0tLS1CRUdJTiBQUklWQVRFIEtFWS0tLS0tCk1JSUV2UUlCQURBTgo=", true)]
    [InlineData("first\rsecond", true)]
    [InlineData("first\nsecond", true)]
    [InlineData("", false)]
    [InlineData("/etc/otel/certs/client.pem", false)]
    [InlineData("C:\\otel certs\\client.key", false)]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.signature", false)]
    public void ContainsInlineCertificateMaterial_ClassifiesValue(string value, bool expected)
        => Assert.Equal(expected, SensitiveDataHelper.ContainsInlineCertificateMaterial(value));
}
