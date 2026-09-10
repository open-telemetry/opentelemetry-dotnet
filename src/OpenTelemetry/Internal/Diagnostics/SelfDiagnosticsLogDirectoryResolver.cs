// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

namespace OpenTelemetry.Internal;

internal static class SelfDiagnosticsLogDirectoryResolver
{
    internal static string? Resolve()
        => Resolve(
            GetCurrentPlatform,
            Environment.GetFolderPath,
            Environment.GetEnvironmentVariable);

    internal static string? Resolve(
        Func<SelfDiagnosticsPlatform> platformProvider,
        Func<Environment.SpecialFolder, string> specialFolderProvider,
        Func<string, string?> environmentVariableProvider)
    {
        try
        {
            return platformProvider() switch
            {
                SelfDiagnosticsPlatform.Windows => ResolveWindows(specialFolderProvider),
#if !NETFRAMEWORK
                SelfDiagnosticsPlatform.MacOS => ResolveMacOS(specialFolderProvider),
                SelfDiagnosticsPlatform.Unix => ResolveUnix(specialFolderProvider, environmentVariableProvider),
#endif
                _ => null,
            };
        }
        catch
        {
            // Self-diagnostics configuration must never prevent application startup.
            return null;
        }
    }

    private static SelfDiagnosticsPlatform GetCurrentPlatform()
    {
#if !NETFRAMEWORK
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SelfDiagnosticsPlatform.Windows;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? SelfDiagnosticsPlatform.MacOS
            : SelfDiagnosticsPlatform.Unix;
#else
        return SelfDiagnosticsPlatform.Windows;
#endif
    }

    private static string? ResolveWindows(Func<Environment.SpecialFolder, string> specialFolderProvider)
    {
        var localAppData = specialFolderProvider(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(localAppData)
            ? null
            : Path.Combine(localAppData, "OpenTelemetry", "dotnet-diagnostics");
    }

#if !NETFRAMEWORK
    private static string? ResolveMacOS(Func<Environment.SpecialFolder, string> specialFolderProvider)
    {
        var home = specialFolderProvider(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home)
            ? null
            : Path.Combine(home, "Library", "Logs", "OpenTelemetry", "dotnet-diagnostics");
    }

    private static string? ResolveUnix(
        Func<Environment.SpecialFolder, string> specialFolderProvider,
        Func<string, string?> environmentVariableProvider)
    {
        var xdgStateHome = environmentVariableProvider("XDG_STATE_HOME");
        if (xdgStateHome is { Length: > 0 } && xdgStateHome[0] == '/')
        {
            return Path.Combine(xdgStateHome, "opentelemetry", "dotnet-diagnostics");
        }

        var home = specialFolderProvider(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home)
            ? null
            : Path.Combine(home, ".local", "state", "opentelemetry", "dotnet-diagnostics");
    }
#endif
}
