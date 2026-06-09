// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Configuration.Declarative.Tests;

internal sealed class DeclarativeYamlTestFileFactory : IDisposable
{
    public DeclarativeYamlTestFileFactory()
    {
        Directory.CreateDirectory(this.TempDirectory);
    }

    public string TempDirectory { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public string CreateYamlFile(string yaml)
    {
        var path = Path.Combine(this.TempDirectory, $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    public string CreateDeclarativeYaml(
        string fileFormat = "1.0",
        bool? disabled = null,
        IReadOnlyDictionary<string, string>? resourceAttributes = null)
    {
        var builder = new StringBuilder();
        builder.Append("file_format: \"").Append(EscapeYaml(fileFormat)).AppendLine("\"");

        if (disabled.HasValue)
        {
            builder.Append("disabled: ").AppendLine(disabled.Value ? "true" : "false");
        }

        if (resourceAttributes != null && resourceAttributes.Count > 0)
        {
            builder.AppendLine("resource:");
            builder.AppendLine("  attributes:");

            foreach (var attribute in resourceAttributes)
            {
                builder.Append("    - name: \"").Append(EscapeYaml(attribute.Key)).AppendLine("\"");
                builder.Append("      value: \"").Append(EscapeYaml(attribute.Value)).AppendLine("\"");
            }
        }

        return this.CreateYamlFile(builder.ToString());
    }

    public void Dispose() => Directory.Delete(this.TempDirectory, recursive: true);

#pragma warning disable CA1307 // Specify StringComparison for clarity
    private static string EscapeYaml(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
#pragma warning restore CA1307 // Specify StringComparison for clarity
}
