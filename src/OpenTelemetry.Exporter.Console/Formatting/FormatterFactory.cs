// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting;

internal static class FormatterFactory
{
    private static Dictionary<string, IFormatterFactory> formatterFactories = new Dictionary<
        string,
        IFormatterFactory
    >(StringComparer.OrdinalIgnoreCase)
    {
        { "simple", new SimpleFormatterFactory() },
        { "keyvalue", new KeyValueFormatterFactory() },
    };

    public static IFormatterFactory GetFormatterFactory(string formatterName)
    {
        if (formatterFactories.TryGetValue(formatterName, out var factory))
        {
            return factory;
        }

        throw new ArgumentOutOfRangeException(
            nameof(formatterName),
            formatterName,
            $"Unknown console formatter '{formatterName}'");
    }
}
