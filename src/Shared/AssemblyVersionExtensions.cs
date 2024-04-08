// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Reflection;

namespace OpenTelemetry.Instrumentation;

internal static class AssemblyVersionExtensions
{
    public static string GetPackageVersion(this Assembly assembly)
    {
        // MinVer https://github.com/adamralph/minver?tab=readme-ov-file#version-numbers
        // together with Microsoft.SourceLink.GitHub https://github.com/dotnet/sourcelink
        // fills AssemblyInformationalVersionAttribute by
        // {majorVersion}.{minorVersion}.{patchVersion}.{pre-release label}.{pre-release version}.{gitHeight}+{Git SHA of current commit}
        // Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4
        // The following parts are optional: pre-release label, pre-release version, git height, Git SHA of current commit
        // For package version, value of AssemblyInformationalVersionAttribute without commit hash is returned.

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var indexOfPlusSign = informationalVersion!.IndexOf('+');
        return indexOfPlusSign > 0
            ? informationalVersion.Substring(0, indexOfPlusSign)
            : informationalVersion;
    }
}
