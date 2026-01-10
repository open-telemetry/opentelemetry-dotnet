// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting;

internal static class FormatterFactory
{
    public const string Compact = "compact";
    public const string Detail = "detail";

    private static Dictionary<string, IFormatterFactory> formatterFactories = new Dictionary<
        string,
        IFormatterFactory
    >(StringComparer.OrdinalIgnoreCase)
    {
        { Compact, new CompactFormatterFactory() },
        { Detail, new DetailFormatterFactory() },
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
