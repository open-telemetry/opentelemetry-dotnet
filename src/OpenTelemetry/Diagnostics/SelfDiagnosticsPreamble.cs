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

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// Builds the one-time preamble written at the top of each self-diagnostics log file.
/// </summary>
internal static class SelfDiagnosticsPreamble
{
    /// <summary>
    /// Builds a multi-line preamble string for a new log file.
    /// </summary>
    /// <returns>A formatted multi-line preamble block for writing as the header of a new log file.</returns>
    internal static string Build()
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

        // OTEL_* environment variable snapshot - only SDK/auto-instrumentation variables;
        // sensitive values (e.g. OTLP auth headers) are redacted.
        sb.AppendLine();
        sb.AppendLine("Environment Variables:");
        AppendEnvVars(sb);

        sb.AppendLine("=== end preamble ===");

        return sb.ToString();
    }

    private static void AppendEnvVars(StringBuilder sb)
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

            // Collect all allowed OTEL_* keys visible across all three scopes so that vars
            // present only in the registry (set after this process started, or cleared via
            // SetEnvironmentVariable) are also surfaced.
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
                var processValue = processVars[k] as string;
                var machineValue = machineVars?[k] as string;
                var userValue = userVars?[k] as string;

                if (processValue != null)
                {
                    var displayValue = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(k, processValue);

                    sb.Append(k).Append(" = ").Append(displayValue);

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
                    var displayValue = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(k, registryValue);
                    var scope = userValue != null ? "user" : "system";

                    sb.Append(k).Append(" = ").Append(displayValue)
                      .Append(" (").Append(scope).Append(" registry - not visible in current process)")
                      .AppendLine();
                }
            }
        }
        catch
        {
            sb.AppendLine("(unable to read environment variables)");
        }
    }

    private static void CollectOtelKeys(IDictionary source, HashSet<string> target)
    {
        foreach (var key in source.Keys)
        {
            if (key is string k
                && k.StartsWith("OTEL_", StringComparison.OrdinalIgnoreCase)
                && SelfDiagnosticsEnvironmentVariablePolicy.IsAllowed(k))
            {
                target.Add(k);
            }
        }
    }

    /// <summary>
    /// Determines the most specific scope that produced the process's effective value for a var.
    /// </summary>
    private static string ClassifySource(string processValue, string? machineValue, string? userValue)
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
}
