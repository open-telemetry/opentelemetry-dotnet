// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Holds the parsed <see cref="DeclarativeConfigurationDocument"/> for a single declarative
/// configuration file, produced at most once across the lifetime of the application.
/// </summary>
/// <remarks>
/// <para>
/// The document is produced on the first call to <see cref="GetDocument()"/>. Every subsequent
/// call returns the same instance.
/// </para>
/// </remarks>
internal sealed class DeclarativeConfigurationDocumentAccessor
{
    private readonly Lazy<DeclarativeConfigurationDocument> document;

    // Claimed by the first caller to reach a cold accessor, so that DocumentParsedOnDemand is
    // emitted at most once.
    private int parseClaimed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationDocumentAccessor"/> class.
    /// </summary>
    /// <param name="filePath">The <see cref="FilePath"/> of the declarative configuration file.</param>
    internal DeclarativeConfigurationDocumentAccessor(FilePath filePath)
        : this(filePath, DeclarativeConfigurationReader.Read)
    {
    }

    internal DeclarativeConfigurationDocumentAccessor(
        FilePath filePath,
        Func<FilePath, DeclarativeConfigurationDocument> readDocument)
    {
        this.FilePath = filePath;
        this.document = new Lazy<DeclarativeConfigurationDocument>(
            () => Parse(filePath, readDocument),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Gets the path of the declarative configuration file.
    /// </summary>
    internal FilePath FilePath { get; }

    /// <summary>
    /// Returns the parsed document, producing it on first call.
    /// </summary>
    /// <returns>The parsed <see cref="DeclarativeConfigurationDocument"/>.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the file cannot be parsed. The exception is cached: subsequent calls
    /// re-throw the same instance without re-reading the file.
    /// </exception>
    internal DeclarativeConfigurationDocument GetDocument() => this.GetDocument(triggeredByProvider: false);

    /// <summary>
    /// Returns the parsed document for a configuration provider, producing it on first call.
    /// </summary>
    /// <returns>The parsed <see cref="DeclarativeConfigurationDocument"/>.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the file cannot be parsed. The exception is cached: subsequent calls
    /// re-throw the same instance without re-reading the file.
    /// </exception>
    internal DeclarativeConfigurationDocument GetDocumentForProvider() =>
        this.GetDocument(triggeredByProvider: true);

    private static DeclarativeConfigurationDocument Parse(
        FilePath filePath,
        Func<FilePath, DeclarativeConfigurationDocument> readDocument)
    {
        try
        {
            return readDocument(filePath);
        }
        catch (DeclarativeConfigurationException ex)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FailedToLoadConfiguration(filePath.DisplayPath, ex);
            throw;
        }
        catch (Exception ex)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FailedToLoadConfiguration(filePath.DisplayPath, ex);
            throw new DeclarativeConfigurationException(
                $"Failed to load declarative configuration from '{filePath.DisplayPath}': {ex.Message}", ex);
        }
    }

    private DeclarativeConfigurationDocument GetDocument(bool triggeredByProvider)
    {
        if (this.document.IsValueCreated)
        {
            return this.document.Value;
        }

        // The claim identifies the first caller to reach the accessor while it was still cold, not
        // necessarily the caller that executed the parse. This design avoids a lock purely for diagnostic
        // purposes, which would be a performance cost for every call to GetDocument().
        var claimedParse = Interlocked.CompareExchange(ref this.parseClaimed, 1, 0) == 0;

        var parsedDocument = this.document.Value;

        if (claimedParse && !triggeredByProvider)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.DocumentParsedOnDemand(this.FilePath.DisplayPath);
        }

        return parsedDocument;
    }
}
