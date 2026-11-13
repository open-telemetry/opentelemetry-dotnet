// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetryLoggerFilterOptions(IOptionsMonitorCache<LoggerFilterOptions> optionsCache)
{
    private readonly Lock syncObject = new();
    private Action<LoggerFilterOptions>[]? configurations;

    public void Configure(LoggerFilterOptions options)
    {
        lock (this.syncObject)
        {
            if (this.configurations != null)
            {
                foreach (var configure in this.configurations)
                {
                    configure(options);
                }
            }
        }
    }

    public void SetConfigurations(List<Action<LoggerFilterOptions>> configurations)
    {
        lock (this.syncObject)
        {
            if (this.configurations != null)
            {
                return;
            }

            // Publish only after all logger options configuration, including
            // PostConfigure callbacks, has completed. Rebuild early monitor
            // options through the normal pipeline instead of modifying filter
            // options that may still be in use or under construction.
            this.configurations = configurations.ToArray();
            if (this.configurations.Length > 0)
            {
                optionsCache.TryRemove(Options.DefaultName);
            }
        }
    }
}
