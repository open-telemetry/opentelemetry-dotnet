// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using NuGet.Versioning;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

internal static class VersionHelper
{
#if BUILDING_USING_PROJECTS
    private static NuGetVersion? apiVersion = new(100, 0, 0);
#else
    private static NuGetVersion? apiVersion;
#endif

    public static NuGetVersion ApiVersion
    {
        get
        {
            return apiVersion ??= ResolveApiVersion();

            static NuGetVersion ResolveApiVersion()
            {
                if (!typeof(TracerProvider).Assembly.TryGetPackageVersion(out var packageVersion))
                {
                    throw new InvalidOperationException("OpenTelemetry.Api package version could not be resolved");
                }

                return NuGetVersion.Parse(packageVersion);
            }
        }
    }

    public static bool IsApiVersionGreaterThanOrEqualTo(int major, int minor)
    {
        return ApiVersion >= new NuGetVersion(major, minor, 0);
    }
}
