// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Configuration.Declarative.Tests;

internal sealed class DeclarativeYamlTestFileFactory : IDisposable
{
    public DeclarativeYamlTestFileFactory()
    {
#if NET
        this.TempDirectory = Directory.CreateTempSubdirectory("otel-decl-cfg-").FullName;
#else
        this.TempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.TempDirectory);
#endif
    }

    public string TempDirectory { get; }

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

    private static string EscapeYaml(string value) => value
#if NET
        .Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase)
        .Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase);
#else
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");
#endif
}
