// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

internal static class SensitiveDataHelper
{
    internal const string RedactedValue = "[REDACTED]";

    private const string PemArmourPrefix = "-----BEGIN";
    private const string Base64PemArmourPrefix = "LS0tLS1CRUdJT";

    private static readonly char[] NewLineChars = ['\r', '\n'];

    internal static bool ContainsInlineCertificateMaterial(string value)
        => value.IndexOfAny(NewLineChars) >= 0
            || Contains(value, PemArmourPrefix)
            || Contains(value, Base64PemArmourPrefix);

    private static bool Contains(string value, string marker)
#if NET || NETSTANDARD2_1_OR_GREATER
        => value.Contains(marker, StringComparison.Ordinal);
#else
        => value.IndexOf(marker, StringComparison.Ordinal) >= 0;
#endif
}
