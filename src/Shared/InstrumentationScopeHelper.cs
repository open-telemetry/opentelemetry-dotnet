// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace OpenTelemetry.Instrumentation;

internal static class InstrumentationScopeHelper
{
    public static string GetVersion<T>()
    {
        return typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    }
}
