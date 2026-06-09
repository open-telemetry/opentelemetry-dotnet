// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

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
    /// The path to the YAML configuration file. May be relative or absolute; it is resolved to a
    /// fully-qualified path.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty or whitespace, or does not end with a
    /// <c>.yaml</c> or <c>.yml</c> extension.
    /// </exception>
    public FilePath(string path)
    {
        Guard.ThrowIfNullOrWhitespace(path);

        if (!string.Equals(System.IO.Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(System.IO.Path.GetExtension(path), ".yml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Declarative configuration file '{path}' must use a .yaml or .yml extension.",
                nameof(path));
        }

        var fullPath = System.IO.Path.GetFullPath(path);

        this.displayPath = path;
        this.Path = fullPath;
        this.normalizedPath = IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }

    /// <summary>
    /// Gets the fully-qualified absolute path to the configuration file.
    /// </summary>
    public string Path { get; }

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
        StringComparer.Ordinal.GetHashCode(this.normalizedPath);

    /// <summary>
    /// Returns the original caller-supplied path for use in diagnostic messages.
    /// </summary>
    /// <returns>
    /// The original path string passed to the constructor.
    /// </returns>
    public override string ToString() => this.displayPath;

    private static bool IsWindows()
    {
#if NET
        return OperatingSystem.IsWindows();
#else
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
    }
}
