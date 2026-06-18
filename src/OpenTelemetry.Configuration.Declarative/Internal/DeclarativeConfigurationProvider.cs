// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// DeclarativeConfigurationException carries the OTEL1006 experimental attribute.
// Suppress once here rather than at every throw/catch site.
#pragma warning disable OTEL1006

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IConfigurationProvider"/> that reads OpenTelemetry configuration from a declarative configuration YAML file.
/// The file is parsed and loaded into the provider's data dictionary.
/// </summary>
internal sealed class DeclarativeConfigurationProvider(FilePath filePath) : ConfigurationProvider
{
    private readonly string fileDisplayPath = filePath.DisplayPath;

    internal FilePath FilePath { get; } = filePath;

    /// <inheritdoc/>
    public override void Load()
    {
        try
        {
            var data = DeclarativeConfigurationReader.Read(this.FilePath);

            this.Data = data;

            OpenTelemetryDeclarativeConfigurationEventSource.Log.ConfigurationLoadSucceeded(this.fileDisplayPath, data.Count);

            if (data.TryGetValue(DeclarativeConfigurationConverter.DisabledKey, out var disabledValue) &&
                bool.TryParse(disabledValue, out var disabled) && disabled)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.SdkDisabledDetected(this.fileDisplayPath);
            }
        }
        catch (DeclarativeConfigurationException ex)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FailedToLoadConfiguration(this.fileDisplayPath, ex);
            throw;
        }
        catch (Exception ex)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FailedToLoadConfiguration(this.fileDisplayPath, ex);
            throw new DeclarativeConfigurationException(
                $"Failed to load declarative configuration from '{this.fileDisplayPath}': {ex.Message}", ex);
        }
    }
}
