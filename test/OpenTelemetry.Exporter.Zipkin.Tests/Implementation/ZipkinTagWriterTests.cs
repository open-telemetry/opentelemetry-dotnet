// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests.Implementation;

public class ZipkinTagWriterTests
{
    [Fact]
    public void KvListTagIsWrittenAsJsonObjectString()
    {
        var kvList = new Dictionary<string, object?>
        {
            ["name"] = "acme",
            ["tier"] = 2L,
            ["nested"] = new Dictionary<string, string> { ["x"] = "y" },
        };

        var json = WriteTag("kvlist", kvList);

        using var document = JsonDocument.Parse(json);
        var tagValue = document.RootElement.GetProperty("kvlist").GetString();
        Assert.NotNull(tagValue);

        using var kvListDocument = JsonDocument.Parse(tagValue);
        var root = kvListDocument.RootElement;

        Assert.Equal("acme", root.GetProperty("name").GetString());

        // Zipkin tag values are strings so nested scalars are written as strings.
        Assert.Equal("2", root.GetProperty("tier").GetString());

        // Nested key/value lists are embedded as JSON strings, mirroring the
        // existing array handling.
        var nested = root.GetProperty("nested").GetString();
        Assert.NotNull(nested);
        using var nestedDocument = JsonDocument.Parse(nested);
        Assert.Equal("y", nestedDocument.RootElement.GetProperty("x").GetString());
    }

    [Fact]
    public void StringDictionaryTagIsWrittenAsJsonObjectString()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["region"] = "eu",
            ["env"] = "prod",
        };

        var json = WriteTag("labels", dictionary);

        using var document = JsonDocument.Parse(json);
        var tagValue = document.RootElement.GetProperty("labels").GetString();
        Assert.NotNull(tagValue);

        using var labelsDocument = JsonDocument.Parse(tagValue);
        Assert.Equal("eu", labelsDocument.RootElement.GetProperty("region").GetString());
        Assert.Equal("prod", labelsDocument.RootElement.GetProperty("env").GetString());
    }

    private static string WriteTag(string key, object? value)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var writerAlias = writer;
        writerAlias.WriteStartObject();

        Assert.True(ZipkinTagWriter.Instance.TryWriteTag(ref writerAlias, key, value));

        writerAlias.WriteEndObject();
        writerAlias.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
