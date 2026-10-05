// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Tests;

internal sealed class CultureSwitcher : IDisposable
{
    private static readonly bool IsInvariant = IsGlobalizationInvariant();
    private readonly CultureInfo previous;

    private CultureSwitcher(string name)
    {
        this.previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
    }

    public static IDisposable UseCulture(string name)
        => IsInvariant ? NullDisposable.Instance : new CultureSwitcher(name);

    public void Dispose()
        => CultureInfo.CurrentCulture = this.previous;

    private static bool IsGlobalizationInvariant()
    {
        // Based on https://www.meziantou.net/detect-globalization-invariant-mode-in-dotnet.htm
        if (AppContext.TryGetSwitch("System.Globalization.Invariant", out bool isEnabled) && isEnabled)
        {
            return true;
        }

        if (Environment.GetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT") is { Length: > 0 } value &&
            (string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase) || value is "1"))
        {
            return true;
        }

        return false;
    }

    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable Instance = new();

        public void Dispose()
        {
            // No-op
        }
    }
}
