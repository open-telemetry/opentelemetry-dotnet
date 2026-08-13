// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
#if !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif
using System.Text;

namespace OpenTelemetry.Internal;

/// <summary>
/// Builds the one-time preamble written at the top of each self-diagnostics log file.
/// </summary>
internal static class SelfDiagnosticsPreamble
{
    // Well-known runtime-level env vars logged unconditionally. Values are always disclosed
    // (GUIDs, paths, 0/1 flags, environment names) because none carry secrets and all are
    // essential for diagnosing profiler attachment, startup injection, and environment config.
    private static readonly string[] RuntimeEnvVarNames =
    [

        // .NET Framework CLR profiler
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH_32",
        "COR_PROFILER_PATH_64",

        // .NET CoreCLR profiler
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",

        // Startup injection mechanisms
        "DOTNET_STARTUP_HOOKS",
        "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES",

        // Runtime environment - affect which config files are loaded
        "DOTNET_ENVIRONMENT",
        "ASPNETCORE_ENVIRONMENT",
        "DOTNET_RUNNING_IN_CONTAINER",

        // Legacy CLR knob still set by some older deployment scripts
        "COMPlus_LoaderOptimization",
    ];

    /// <summary>
    /// Builds a multi-line preamble string for a new log file.
    /// </summary>
    /// <param name="configuration">
    /// The configuration in effect, which determines how much of the <c>OTEL_*</c> environment
    /// variable snapshot is disclosed and supplies any environment variable parse warnings to
    /// report.
    /// </param>
    /// <returns>A formatted multi-line preamble block for writing as the header of a new log file.</returns>
    internal static string Build(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== OpenTelemetry .NET SDK self-diagnostics ===");
        sb.Append("SDK version          : ").AppendLine(Sdk.InformationalVersion);
        sb.Append("DateTime (UTC)       : ").AppendLine(DateTime.UtcNow.ToString("O"));

        // Runtime info
#if !NETFRAMEWORK
        sb.Append("Runtime              : ").AppendLine(RuntimeInformation.FrameworkDescription);
        sb.Append("CLR version          : ").AppendLine(Environment.Version.ToString());
        sb.Append("OS                   : ").AppendLine(RuntimeInformation.OSDescription);
        sb.Append("Architecture         : ").AppendLine(RuntimeInformation.ProcessArchitecture.ToString());
#if NET
        sb.Append("Runtime ID           : ").AppendLine(RuntimeInformation.RuntimeIdentifier);
#endif
#else
        sb.Append("Runtime              : .NET Framework ").AppendLine(Environment.Version.ToString());
        sb.Append("CLR version          : ").AppendLine(Environment.Version.ToString());
        sb.Append("OS                   : ").AppendLine(Environment.OSVersion.ToString());
#endif

        // Process info
        try
        {
            using var process = Process.GetCurrentProcess();
            sb.Append("Process ID           : ").AppendLine(process.Id.ToString(CultureInfo.InvariantCulture));
            sb.Append("Process name         : ").AppendLine(process.ProcessName);
#if NET
            sb.Append("Process start time   : ").AppendLine(process.StartTime.ToUniversalTime().ToString("O"));
#endif
            sb.Append("Process working set  : ").Append(process.WorkingSet64.ToString(CultureInfo.InvariantCulture)).AppendLine(" bytes");
            sb.Append("Thread count         : ").AppendLine(process.Threads.Count.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // Not all environments support process access
        }

#if NET
        try
        {
            var processPath = Environment.ProcessPath;
            if (processPath is not null)
            {
                sb.Append("Process path         : ").AppendLine(processPath);
            }
        }
        catch
        {
            // ignored
        }
#endif

        // Entry assembly
        try
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry is not null)
            {
                sb.Append("Entry assembly       : ").AppendLine(entry.FullName);
            }
        }
        catch
        {
            // ignored
        }

        // System / environment info
        try
        {
            sb.AppendLine();
            sb.Append("Machine name         : ").AppendLine(Environment.MachineName);
            sb.Append("Processor count      : ").AppendLine(Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture));
            sb.Append("64-bit OS            : ").AppendLine(Environment.Is64BitOperatingSystem.ToString());
            sb.Append("64-bit process       : ").AppendLine(Environment.Is64BitProcess.ToString());
            sb.Append("Server GC            : ").AppendLine(GCSettings.IsServerGC.ToString());
            sb.Append("GC latency mode      : ").AppendLine(GCSettings.LatencyMode.ToString());
            sb.Append("App base directory   : ").AppendLine(AppDomain.CurrentDomain.BaseDirectory);
            sb.Append("Working directory    : ").AppendLine(Environment.CurrentDirectory);
            if (!string.IsNullOrEmpty(configuration.LogDirectory))
            {
                sb.Append("Log directory        : ").AppendLine(configuration.LogDirectory);
            }
#if NET
            sb.Append("Dynamic code         : ").AppendLine(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported.ToString());
#endif
        }
        catch
        {
            // ignored
        }

#if NET
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            sb.Append("GC heap committed    : ").Append((gcInfo.TotalCommittedBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture)).AppendLine(" MiB");
            sb.Append("GC memory limit      : ").Append((gcInfo.TotalAvailableMemoryBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture)).AppendLine(" MiB");
        }
        catch
        {
            // ignored
        }
#endif

        // Runtime env vars suppressed when mode is None, consistent with the OTEL_* section.
        if (configuration.EnvironmentVariables != EnvironmentVariableLogMode.None)
        {
            AppendRuntimeEnvVars(sb);
        }

        // Environment variable parse failures are reported here because options are constructed
        // before any sink exists, so there is nowhere else for them to surface.
        if (configuration.ConfigurationWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Configuration Warnings:");
            foreach (var warning in configuration.ConfigurationWarnings)
            {
                sb.Append("- ").AppendLine(warning);
            }
        }

        // OTEL_* environment variable snapshot. Names of all OTEL_* variables are listed; how
        // much of each value is disclosed depends on the configured mode.
        if (configuration.EnvironmentVariables != EnvironmentVariableLogMode.None)
        {
            sb.AppendLine();
            sb.Append("Environment Variables (mode: ")
              .Append(configuration.EnvironmentVariables)
              .AppendLine("):");
            AppendEnvVars(sb, configuration.EnvironmentVariables);
        }

        sb.AppendLine("=== end preamble ===");

        return sb.ToString();
    }

    internal static string ClassifySource(string processValue, string? machineValue, string? userValue)
    {
        // Known ambiguity: if Machine=A, User=B (B!=A), and the process has A,
        // we cannot distinguish "process explicitly set it to A" from "User registry was changed to B
        // after process start and the process still carries the old Machine value A". Both resolve to
        // "process", which is the safe, conservative label.

        if (machineValue == null && userValue == null)
        {
            // Not present in either registry scope: set exclusively at process level
            // (e.g. launchSettings.json, docker ENV, explicit SetEnvironmentVariable call).
            return "process";
        }

        // What the process should have inherited: User shadows Machine at logon.
        var expectedFromRegistry = userValue ?? machineValue;

        if (processValue == expectedFromRegistry)
        {
            // Process value matches what the registry would have provided - cleanly inherited,
            // no process-level override.
            return userValue != null ? "user" : "system";
        }

        // Process value differs from the expected registry inheritance, so either the process
        // overrode it, or the registry changed after process start (see Scenario H in remarks).
        return "process";
    }

    private static void AppendRuntimeEnvVars(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("Runtime environment variables:");

        var anySet = false;
        foreach (var name in RuntimeEnvVarNames)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value is not null)
            {
                sb.Append(name).Append(" = ").AppendLine(value);
                anySet = true;
            }
        }

        if (!anySet)
        {
            sb.AppendLine("(none set)");
        }
    }

    private static void AppendEnvVars(StringBuilder sb, EnvironmentVariableLogMode mode)
    {
        try
        {
#if NETFRAMEWORK
            // .NET Framework is Windows-only; RuntimeInformation is unavailable on net462.
            const bool isWindows = true;
#else
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
            var processVars = Environment.GetEnvironmentVariables();

            // Machine and User targets read the Windows registry. On non-Windows they return
            // empty without throwing, but source detection only makes sense on Windows.
            IDictionary? machineVars = null;
            IDictionary? userVars = null;

            if (isWindows)
            {
                // Wrap each separately so a permission failure on one scope doesn't
                // prevent the other from being read.
                try
                {
                    machineVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
                }
                catch
                {
                    // Registry inaccessible; Machine scope omitted from source detection.
                }

                try
                {
                    userVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
                }
                catch
                {
                    // Registry inaccessible; User scope omitted from source detection.
                }
            }

            // Collect all OTEL_* keys visible across all three scopes so that vars present only
            // in the registry (set after this process started, or cleared via
            // SetEnvironmentVariable) are also surfaced. Names are never filtered: an
            // unrecognised name is exactly what makes a typo visible.
            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectOtelKeys(processVars, allKeys);
            if (machineVars != null)
            {
                CollectOtelKeys(machineVars, allKeys);
            }

            if (userVars != null)
            {
                CollectOtelKeys(userVars, allKeys);
            }

            if (allKeys.Count == 0)
            {
                sb.AppendLine("(none set)");
                return;
            }

            var sortedKeys = new List<string>(allKeys);
            sortedKeys.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var k in sortedKeys)
            {
                // null means the key is absent from that scope (IDictionary returns null for missing keys).
                var machineValue = machineVars?[k] as string;
                var userValue = userVars?[k] as string;

                if (processVars[k] is string processValue)
                {
                    AppendNameAndValue(sb, mode, k, processValue);

                    if (isWindows)
                    {
                        sb.Append(" (source: ")
                          .Append(ClassifySource(processValue, machineValue, userValue))
                          .Append(')');
                    }

                    sb.AppendLine();
                }
                else
                {
                    // Var is in the registry but absent from the process env block.
                    // This happens when the var was set in the registry after this process
                    // started, or when it was explicitly cleared with SetEnvironmentVariable(k, null).
                    // User shadows Machine, so prefer User value if both are present.
                    var registryValue = userValue ?? machineValue ?? string.Empty;
                    var scope = userValue != null ? "user" : "system";

                    AppendNameAndValue(sb, mode, k, registryValue);

                    sb.Append(" (").Append(scope).Append(" registry - not visible in current process)")
                      .AppendLine();
                }
            }
        }
        catch
        {
            sb.AppendLine("(unable to read environment variables)");
        }
    }

    /// <summary>
    /// Appends a variable name, and as much of its value as the mode permits.
    /// </summary>
    private static void AppendNameAndValue(
        StringBuilder sb,
        EnvironmentVariableLogMode mode,
        string name,
        string value)
    {
        sb.Append(name);

        if (mode == EnvironmentVariableLogMode.Names)
        {
            return;
        }

        sb.Append(" = ").Append(
            mode == EnvironmentVariableLogMode.AllValues
                ? value
                : SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(name, value));
    }

    private static void CollectOtelKeys(IDictionary source, HashSet<string> target)
    {
        foreach (var key in source.Keys)
        {
            if (key is string k
                && k.StartsWith("OTEL_", StringComparison.OrdinalIgnoreCase))
            {
                target.Add(k);
            }
        }
    }
}
