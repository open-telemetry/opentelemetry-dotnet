// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Runtime.InteropServices;
#endif
using OpenTelemetry.Internal;
using static System.IO.Path;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Represents a validated, normalized path to an OpenTelemetry declarative configuration file.
/// </summary>
/// <remarks>
/// The constructor resolves the supplied path to a fully-qualified absolute path. Equality is
/// file-identity: two <see cref="FilePath"/> values are equal when they resolve to the same file
/// regardless of whether the original inputs were relative, absolute, or differently cased on a
/// case-insensitive file system. <see cref="ToString"/> returns the original caller-supplied
/// path so that diagnostic messages remain readable.
/// </remarks>
internal readonly record struct FilePath
{
    private readonly string normalizedPath;

    // original, for messages
    private readonly string displayPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePath"/> struct.
    /// </summary>
    /// <param name="path">
    /// The path to the YAML configuration file. May be relative or absolute; relative paths are
    /// resolved against <see cref="AppContext.BaseDirectory"/> (the application directory) so
    /// that resolution is correct under IIS hosting where <c>Environment.CurrentDirectory</c>
    /// may point to the IIS worker process directory rather than the application directory.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty or whitespace, or does not end with a
    /// <c>.yaml</c> or <c>.yml</c> extension.
    /// </exception>
    public FilePath(string path)
    {
        Guard.ThrowIfNullOrWhitespace(path);

        // Spec: YAML configuration files MUST use file extensions .yaml or .yml.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/data-model.md#yaml-file-format
        var extension = GetExtension(path);
        if (!string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Declarative configuration file '{path}' must use a .yaml or .yml extension.",
                nameof(path));
        }

        // Resolve relative paths against AppContext.BaseDirectory, not Environment.CurrentDirectory:
        // under IIS in-process hosting CurrentDirectory is the worker-process directory, not the app directory.
        // The two-arg Path.GetFullPath(path, basePath) overload is unavailable on netstandard2.0/net462, so we combine manually.
        // IsPathRooted is not sufficient on Windows: root-relative (\otel.yaml) and drive-relative (C:otel.yaml)
        // paths return true but are not fully qualified, so they would resolve against ambient process state
        // (current drive / current directory on that drive). Use IsPathFullyQualified on modern .NET and a
        // manual equivalent on older TFMs so those paths are combined with AppContext.BaseDirectory instead.
        var fullPath = IsFullyQualified(path)
            ? GetFullPath(path)
            : GetFullPath(Combine(AppContext.BaseDirectory, path));

        this.displayPath = path;
        this.Path = fullPath;
        this.normalizedPath = IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }

    /// <summary>
    /// Gets the fully-qualified absolute path to the configuration file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the original caller-supplied path, for use in diagnostic messages.
    /// </summary>
    public string DisplayPath => this.displayPath;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="other"/> resolves to the same file
    /// as this instance, regardless of whether the original inputs were relative, absolute, or
    /// differently cased on a case-insensitive file system.
    /// </summary>
    /// <param name="other">The <see cref="FilePath"/> to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both values refer to the same file; otherwise <see langword="false"/>.
    /// </returns>
    public bool Equals(FilePath other) =>
        string.Equals(this.normalizedPath, other.normalizedPath, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.normalizedPath is null ? 0 : StringComparer.Ordinal.GetHashCode(this.normalizedPath);

    /// <summary>
    /// Returns the original caller-supplied path for use in diagnostic messages.
    /// </summary>
    /// <returns>
    /// The original path string passed to the constructor.
    /// </returns>
    public override string ToString() => this.DisplayPath;

    // Returns true when path is fully qualified (no ambient state needed to resolve it).
    // Path.IsPathRooted returns true for root-relative (\otel.yaml) and drive-relative (C:otel.yaml)
    // paths on Windows, but those still depend on current-drive / current-directory state.
    // Path.IsPathFullyQualified (net5+) handles this correctly; the #else branch replicates its
    // Windows logic: require drive-letter + separator (C:\) or UNC prefix (\\).
    private static bool IsFullyQualified(string path)
    {
#if NET
        return IsPathFullyQualified(path);
#else
        if (!IsWindows())
        {
            return IsPathRooted(path);
        }

        // Windows: drive-letter + separator (C:\, C:/) or UNC (\\, //).
        if (path.Length >= 3
            && path[1] == ':'
            && (path[2] == DirectorySeparatorChar || path[2] == AltDirectorySeparatorChar))
        {
            return true;
        }

        return path.Length >= 2
            && (path[0] == DirectorySeparatorChar || path[0] == AltDirectorySeparatorChar)
            && (path[1] == DirectorySeparatorChar || path[1] == AltDirectorySeparatorChar);
#endif
    }

    private static bool IsWindows() =>
#if NET
        OperatingSystem.IsWindows();
#else
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
}
