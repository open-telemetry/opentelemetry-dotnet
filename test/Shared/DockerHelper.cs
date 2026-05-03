// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;

namespace OpenTelemetry.Tests;

/// <summary>
/// Determines if a required Docker engine is available.
/// </summary>
internal static class DockerHelper
{
    /// <summary>
    /// Gets whether the specified Docker platform is available.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the specified Docker platform is available, otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsAvailable(DockerPlatform dockerPlatform)
    {
        const string executable = "docker";

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        void AppendStdout(object sender, DataReceivedEventArgs e)
        {
            stdout.Append(e.Data);
        }

        void AppendStderr(object sender, DataReceivedEventArgs e)
        {
            stderr.Append(e.Data);
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = string.Join(" ", "version", "--format '{{.Server.Os}}'"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process
        {
            StartInfo = processStartInfo,
        };
        process.OutputDataReceived += AppendStdout;
        process.ErrorDataReceived += AppendStderr;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Thrown if Docker is not installed
            return false;
        }
        finally
        {
            process.OutputDataReceived -= AppendStdout;
            process.ErrorDataReceived -= AppendStderr;
        }

        return process.ExitCode == 0 && stdout.ToString().IndexOf(dockerPlatform.ToString(), StringComparison.OrdinalIgnoreCase) > 0;
    }
}
