// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace OpenTelemetry.Internal;

internal static class AssemblyVersionExtensions
{
    public static string GetPackageVersion(this Assembly assembly)
    {
        Debug.Assert(assembly != null, "assembly was null");

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Debug.Assert(!string.IsNullOrEmpty(informationalVersion), "AssemblyInformationalVersionAttribute was not found in assembly");

        return ParsePackageVersion(informationalVersion!);
    }

    public static bool TryGetPackageVersion(this Assembly assembly, [NotNullWhen(true)] out string? packageVersion)
    {
        Debug.Assert(assembly != null, "assembly was null");

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informationalVersion))
        {
            packageVersion = null;
            return false;
        }

        packageVersion = ParsePackageVersion(informationalVersion!);
        return true;
    }

    private static string ParsePackageVersion(string informationalVersion)
    {
        // MinVer https://github.com/adamralph/minver?tab=readme-ov-file#version-numbers
        // together with Microsoft.SourceLink.GitHub https://github.com/dotnet/sourcelink
        // fills AssemblyInformationalVersionAttribute by
        // {majorVersion}.{minorVersion}.{patchVersion}.{pre-release label}.{pre-release version}.{gitHeight}+{Git SHA of current commit}
        // Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4
        // The following parts are optional: pre-release label, pre-release version, git height, Git SHA of current commit
        // For package version, value of AssemblyInformationalVersionAttribute without commit hash is returned.

#if NET || NETSTANDARD2_1_OR_GREATER
        var indexOfPlusSign = informationalVersion.IndexOf('+', StringComparison.Ordinal);
#else
        var indexOfPlusSign = informationalVersion.IndexOf('+');
#endif
        return indexOfPlusSign > 0
            ? informationalVersion.Substring(0, indexOfPlusSign)
            : informationalVersion;
    }
}
