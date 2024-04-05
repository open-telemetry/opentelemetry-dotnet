// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace OpenTelemetry.Instrumentation;

internal static class InstrumentationScopeHelper
{
    public static string GetPackageVersion<T>()
    {
        // MinVer https://github.com/adamralph/minver?tab=readme-ov-file#version-numbers
        // together with Microsoft.SourceLink.GitHub https://github.com/dotnet/sourcelink
        // fills AssemblyInformationalVersionAttribute by
        // `{NuGetPackageVersion}+{CommitHash}`, e.g. `1.7.0-beta.1.86+33d5521a73e881ac59d4bf1213765270ec2422ff`.
        // For Scope version, value of AssemblyInformationalVersionAttribute  without commit hash is returned.
        return typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    }
}
