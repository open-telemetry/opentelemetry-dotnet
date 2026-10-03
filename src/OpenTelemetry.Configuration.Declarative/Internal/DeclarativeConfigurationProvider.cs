// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IConfigurationProvider"/> that reads OpenTelemetry configuration from a
/// declarative configuration YAML file. The file is read at most once; subsequent
/// <see cref="Load"/> calls are ignored.
/// </summary>
internal sealed class DeclarativeConfigurationProvider(DeclarativeConfigurationDocumentAccessor accessor) : ConfigurationProvider
{
    private bool loaded;

    internal FilePath FilePath => accessor.FilePath;

    internal DeclarativeConfigurationDocumentAccessor Accessor => accessor;

    /// <inheritdoc/>
    public override void Load()
    {
        if (this.loaded)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.ConfigurationReloadIgnored(this.FilePath.DisplayPath);
            return;
        }

        var document = accessor.GetDocumentForProvider(); // may throw; propagates as-is

        this.Data = document.FlatKeys;
        this.loaded = true;

        OpenTelemetryDeclarativeConfigurationEventSource.Log.ConfigurationLoadSucceeded(
            this.FilePath.DisplayPath, document.FlatKeys.Count);

        if (document.FlatKeys.TryGetValue(DeclarativeConfigurationConverter.DisabledKey, out var disabledValue) &&
            bool.TryParse(disabledValue, out var disabled) && disabled)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.SdkDisabledDetected(this.FilePath.DisplayPath);
        }
    }

    /// <inheritdoc/>
    public override bool TryGet(string key, out string? value)
    {
        if (this.Data.TryGetValue(key, out value))
        {
            return true; // the document supplies it
        }

        if (StrictModeKeyScope.IsInScope(key))
        {
            value = null; // earlier sources are not consulted for this key
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
    {
        // Hide earlier sources' in-scope keys from enumeration, so GetChildren(), AsEnumerable()
        // and GetSection(...).Exists() agree with TryGet. The document's own keys are added by
        // the base implementation.
        if (parentPath is null)
        {
            earlierKeys = earlierKeys.Where(key => !StrictModeKeyScope.IsInScope(key));
        }
        else if (StrictModeKeyScope.IsInScope(parentPath))
        {
            earlierKeys = [];
        }

        return base.GetChildKeys(earlierKeys, parentPath);
    }
}
