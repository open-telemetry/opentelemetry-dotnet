// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ConfigurationSchemaReaderTests
{
    [Theory]
    [InlineData("""{"patternProperties":{}}""")]
    [InlineData("""{"allOf":[]}""")]
    [InlineData("""{"anyOf":[]}""")]
    [InlineData("""{"unevaluatedProperties":false}""")]
    [InlineData("""{"propertyNames":{}}""")]
    [InlineData("""{"if":{}}""")]
    [InlineData("""{"const":1}""")]
    [InlineData("""{"properties":{"a":{"allOf":[]}}}""")]
    [InlineData("""{"properties":{"a":{"oneOf":[{"anyOf":[]}]}}}""")]
    public void Read_UnsupportedKeyword_Throws(string schema) => AssertUnsupported(schema);

    [Theory]
    [InlineData("""{"properties":{"a":{"$ref":"https://example.com/schema.json"}}}""")]
    [InlineData("""{"properties":{"a":{"$ref":"#/$defs/Missing"}}}""")]
    [InlineData("""{"properties":{"a":{"$ref":"#/properties/b"}}}""")]
    [InlineData("""{"$defs":{"A":{}},"properties":{"a":{"$ref":"#/$defs/a"}}}""")]
    [InlineData("""{"$defs":{"A":{"type":"object"}},"properties":{"a":{"$ref":"#/$defs/A","type":"object"}}}""")]
    public void Read_UnsupportedReference_Throws(string schema) => AssertUnsupported(schema);

    [Theory]
    [InlineData("""{"oneOf":[]}""")]
    [InlineData("""{"oneOf":{}}""")]
    public void Read_OneOfWithMalformedShape_Throws(string schema) =>
        AssertUnsupported(schema, "'oneOf' must contain a non-empty array.");

    [Theory]
    [InlineData("""{"oneOf":[null]}""")]
    [InlineData("""{"oneOf":["string"]}""")]
    [InlineData("""{"oneOf":[[]]}""")]
    public void Read_OneOfWithNonSchemaAlternative_Throws(string schema) =>
        AssertUnsupported(schema, "'oneOf' must contain a schema object.");

    [Theory]
    [InlineData("""{"oneOf":[{}]}""")]
    [InlineData("""{"oneOf":[{"type":"object"}]}""")]
    [InlineData("""{"oneOf":[{"type":["string","object"]}]}""")]
    [InlineData("""{"oneOf":[{"type":"array","items":{}}]}""")]
    [InlineData("""{"oneOf":[{"type":"array","items":{"type":"object"}}]}""")]
    [InlineData("""{"oneOf":[{"type":"array","items":{"type":"array","items":{"type":"object"}}}]}""")]
    [InlineData("""{"oneOf":[{"type":"string","properties":{"a":{"type":"string"}}}]}""")]
    [InlineData("""{"oneOf":[{"type":"string","additionalProperties":false}]}""")]
    [InlineData("""{"oneOf":[{"type":"string","additionalProperties":{"type":"string"}}]}""")]
    [InlineData("""{"oneOf":[{"oneOf":[{"type":"string"}]}]}""")]
    [InlineData("""{"$defs":{"A":{"oneOf":[{"$ref":"#/$defs/B"}]},"B":{"type":"object"}}}""")]
    [InlineData("""{"$defs":{"B":{"type":"object"},"A":{"oneOf":[{"$ref":"#/$defs/B"}]}}}""")]
    [InlineData("""{"$defs":{"B":{"type":"object"}},"oneOf":[{"type":"array","items":{"$ref":"#/$defs/B"}}]}""")]
    [InlineData("""{"$defs":{"A":{"type":"array","items":{"$ref":"#/$defs/B"}},"B":{"type":["string","object"]}},"oneOf":[{"$ref":"#/$defs/A"}]}""")]
    public void Read_OneOfWithUnsupportedAlternativeContent_Throws(string schema) =>
        AssertUnsupported(schema, "'oneOf' alternatives must have explicit non-object types and no property constraints.");

    [Theory]
    [InlineData("""{"oneOf":[{"type":"array"}]}""")]
    [InlineData("""{"oneOf":[{"type":["string","array"]}]}""")]
    public void Read_OneOfWithArrayAlternativeWithoutItems_Throws(string schema) =>
        AssertUnsupported(schema, "'oneOf' array alternatives must define non-object items.");

    [Theory]
    [InlineData("""{"oneOf":[{"type":"string"}],"type":"string"}""", "type")]
    [InlineData("""{"oneOf":[{"type":"string"}],"properties":{}}""", "properties")]
    [InlineData("""{"oneOf":[{"type":"string"}],"items":{"type":"string"}}""", "items")]
    [InlineData("""{"oneOf":[{"type":"string"}],"additionalProperties":false}""", "additionalProperties")]
    [InlineData("""{"properties":{"a":{"oneOf":[{"type":"string"}],"type":"string"}}}""", "type")]
    public void Read_OneOfWithSiblingConstraint_Throws(string schema, string keyword) =>
        AssertUnsupported(schema, $"'oneOf' has the sibling keyword '{keyword}'.");

    [Theory]
    [InlineData("""{"type":[]}""")]
    [InlineData("""{"type":["string","string"]}""")]
    [InlineData("""{"additionalProperties":"false"}""")]
    [InlineData("""{"additionalProperties":"true"}""")]
    [InlineData("""{"additionalProperties":!!str false}""")]
    [InlineData("""{"additionalProperties":!!str true}""")]
    [InlineData("""{"additionalProperties":!!bool false}""")]
    public void Read_MalformedConstraint_Throws(string schema) => AssertUnsupported(schema);

    [Theory]
    [InlineData("""{"type":null}""", "type")]
    [InlineData("""{"type":true}""", "type")]
    [InlineData("""{"type":3}""", "type")]
    [InlineData("""{"type":string}""", "type")]
    [InlineData("""{"type":'null'}""", "type")]
    [InlineData("""{"type":!!str "null"}""", "type")]
    [InlineData("""{"type":!!null "null"}""", "type")]
    [InlineData("""{"type":[null]}""", "type")]
    [InlineData("""{"type":["string",null]}""", "type")]
    [InlineData("""{"type":[true]}""", "type")]
    [InlineData("""{"type":[3]}""", "type")]
    [InlineData("""{"type":['string']}""", "type")]
    [InlineData("""{type:"string"}""", "keyword")]
    [InlineData("""{'type':"string"}""", "keyword")]
    [InlineData("""{!!str "type":"string"}""", "keyword")]
    [InlineData("""{"properties":{null: {}}}""", "keyword")]
    [InlineData("""{"properties":{true: {}}}""", "keyword")]
    [InlineData("""{"properties":{42: {}}}""", "keyword")]
    [InlineData("""{"properties":{a: {}}}""", "keyword")]
    [InlineData("""{"$defs":{null: {}}}""", "keyword")]
    [InlineData("""{"$defs":{A: {}}}""", "keyword")]
    [InlineData("""{"$defs":{"A":{}},"properties":{"a":{"$ref":'#/$defs/A'}}}""", "$ref")]
    [InlineData("""{"$defs":{"A":{}},"properties":{"a":{"$ref":!!str "#/$defs/A"}}}""", "$ref")]
    [InlineData("""{"$defs":{"A":{}},"properties":{"a":{$ref:"#/$defs/A"}}}""", "keyword")]
    public void Read_NonJsonString_Throws(string schema, string keyword) =>
        AssertUnsupported(schema, $"'{keyword}' must contain a double-quoted JSON string.");

    [Theory]
    [InlineData("array", (int)SchemaValueTypes.Array)]
    [InlineData("boolean", (int)SchemaValueTypes.Boolean)]
    [InlineData("integer", (int)SchemaValueTypes.Integer)]
    [InlineData("null", (int)SchemaValueTypes.Null)]
    [InlineData("number", (int)SchemaValueTypes.Number)]
    [InlineData("object", (int)SchemaValueTypes.Object)]
    [InlineData("string", (int)SchemaValueTypes.String)]
    public void Read_Type_MapsToTheExpectedFlag(string type, int expected) =>
        Assert.Equal((SchemaValueTypes)expected, Read($$"""{"type":"{{type}}"}""").ValueTypes);

    [Fact]
    public void Read_TypeArray_CombinesTheFlags()
    {
        const string schema = """{"type":["array","boolean","integer","null","number","object","string"]}""";
        const SchemaValueTypes expected = SchemaValueTypes.Array | SchemaValueTypes.Boolean | SchemaValueTypes.Integer
            | SchemaValueTypes.Null | SchemaValueTypes.Number | SchemaValueTypes.Object | SchemaValueTypes.String;

        Assert.Equal(expected, Read(schema).ValueTypes);
    }

    [Theory]
    [InlineData("""{"type":"\u0073tring"}""", (int)SchemaValueTypes.String)]
    [InlineData("""{"type":"\u006eull"}""", (int)SchemaValueTypes.Null)]
    public void Read_EscapedJsonStringTypes_AreAccepted(string schema, int expected) =>
        Assert.Equal((SchemaValueTypes)expected, Read(schema).ValueTypes);

    [Fact]
    public void Read_JsonStringNamesAndReferences_AreAccepted()
    {
        const string schema = """
            {
              "$defs": {"null": {"type": "null"}},
              "properties": {
                "null": {"$ref": "#/$defs/null"},
                "true": {},
                "42": {},
                "\u0061": {}
              }
            }
            """;

        var root = Read(schema);

        Assert.Equal(["42", "a", "null", "true"], root.Properties.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(SchemaValueTypes.Null, root.Properties["null"].ValueTypes);
    }

    [Fact]
    public void Read_PropertyAndDefinitionNames_AreCaseSensitive()
    {
        const string schema = """
            {
              "$defs": {
                "A": {"type": "string"},
                "a": {"type": "integer"}
              },
              "properties": {
                "A": {"$ref": "#/$defs/A"},
                "a": {"$ref": "#/$defs/a"}
              }
            }
            """;

        var root = Read(schema);

        Assert.Equal(["A", "a"], root.Properties.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(SchemaValueTypes.String, root.Properties["A"].ValueTypes);
        Assert.Equal(SchemaValueTypes.Integer, root.Properties["a"].ValueTypes);
        Assert.NotSame(root.Properties["A"], root.Properties["a"]);
    }

    [Fact]
    public void Read_ScalarAndScalarArrayAlternatives_AreAccepted()
    {
        const string schema = """
            {
              "$defs": {
                "A": {"oneOf": [{"$ref": "#/$defs/B"}, {"type": "array", "items": {"$ref": "#/$defs/B"}}]},
                "B": {"type": ["string", "null"]}
              },
              "properties": {"a": {"$ref": "#/$defs/A"}}
            }
            """;

        var root = Read(schema);

        Assert.True(root.Properties["a"].HasOneOf, "Expected property 'a' to have oneOf alternatives.");
    }

    [Fact]
    public void Read_NestedScalarArrayAlternative_IsAccepted()
    {
        const string schema = """
            {
              "oneOf": [
                {"type": "array", "items": {"type": "array", "items": {"type": "string"}}},
                {"type": "string"}
              ]
            }
            """;

        var root = Read(schema);

        Assert.True(root.HasOneOf, "Expected the root schema to have oneOf alternatives.");
    }

    [Fact]
    public void Read_RecursiveArrayAlternative_DoesNotLoop()
    {
        const string schema = """
            {
              "$defs": {
                "A": {"type": "array", "items": {"$ref": "#/$defs/A"}}
              },
              "oneOf": [{"$ref": "#/$defs/A"}, {"type": "string"}]
            }
            """;

        var root = Read(schema);

        Assert.True(root.HasOneOf, "Expected the root schema to have oneOf alternatives.");
    }

    [Theory]
    [InlineData("""{"type":"widget"}""")]
    [InlineData("""{"additionalProperties":"maybe"}""")]
    [InlineData("""{"additionalProperties":[]}""")]
    [InlineData("""{"properties":[]}""")]
    [InlineData("""{"items":"string"}""")]
    [InlineData("""{"$defs":[]}""")]
    [InlineData("""{"properties":{"a":{"$defs":{}}}}""")]
    [InlineData("""{"properties":{"a":{"$schema":"x"}}}""")]
    [InlineData("""{"$defs":{"A":{"properties":{"b":{"$defs":{}}}}}}""")]
    [InlineData("""{"properties":{"a":{"$ref":"#/$defs/A"}},"$defs":{"A":{"$schema":"x"}}}""")]
    [InlineData("""{"properties":{"a":{"items":{"$ref":"#/$defs/Missing"}}}}""")]
    [InlineData("""{"additionalProperties":{"$ref":"#/$defs/Missing"}}""")]
    public void Read_MalformedSchema_Throws(string schema) => AssertUnsupported(schema);

    [Theory]
    [InlineData("""[]""")]
    [InlineData("")]
    public void Read_NotASingleJsonObject_Throws(string schema)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Read(schema));

        Assert.Equal("The configuration schema must be a single JSON object.", exception.Message);
    }

    [Theory]
    [InlineData("""{"type":"object"} {"type":"object"}""")]
    [InlineData("""{"properties":{"a":{},"a":{}}}""")]
    [InlineData("""{"$defs":{"A":{},"A":{}}}""")]
    public void Read_InvalidSyntax_Throws(string schema)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Read(schema));

        Assert.StartsWith("The configuration schema is not valid JSON:", exception.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<YamlDotNet.Core.YamlException>(exception.InnerException);
    }

    [Fact]
    public void Read_EmptySchema_UsesPermissiveDefaults()
    {
        var root = Read("{}");

        Assert.Equal(SchemaValueTypes.None, root.ValueTypes);
        Assert.Equal(AdditionalPropertiesKind.Allowed, root.AdditionalProperties);
        Assert.Null(root.AdditionalPropertiesSchema);
        Assert.Null(root.Items);
        Assert.False(root.HasOneOf);
        Assert.Empty(root.Properties);
    }

    [Fact]
    public void Read_ReferencesInItemsAndAdditionalProperties_ResolveToTheSharedNode()
    {
        const string schema = """
            {
              "$defs": {"A": {"type": "object", "additionalProperties": false}},
              "properties": {
                "list": {"type": "array", "items": {"$ref": "#/$defs/A"}},
                "map": {"type": "object", "additionalProperties": {"$ref": "#/$defs/A"}}
              }
            }
            """;

        var root = Read(schema);

        var items = root.Properties["list"].Items;
        Assert.NotNull(items);
        Assert.Equal(AdditionalPropertiesKind.Forbidden, items.AdditionalProperties);
        Assert.Equal(AdditionalPropertiesKind.Schema, root.Properties["map"].AdditionalProperties);
        Assert.Same(items, root.Properties["map"].AdditionalPropertiesSchema);
    }

    [Fact]
    public void Read_ReferenceBeforeItsDefinition_ResolvesToTheSharedNode()
    {
        const string schema = """
            {
              "properties": {"a": {"$ref": "#/$defs/B"}, "b": {"$ref": "#/$defs/B", "description": "ignored", "deprecated": true}},
              "$defs": {"B": {"type": ["object", "null"], "properties": {"self": {"$ref": "#/$defs/B"}}, "additionalProperties": false}}
            }
            """;

        var root = Read(schema);

        var b = root.Properties["a"];
        Assert.Same(b, root.Properties["b"]);
        Assert.Same(b, b.Properties["self"]);
        Assert.Equal(SchemaValueTypes.Object | SchemaValueTypes.Null, b.ValueTypes);
        Assert.Equal(AdditionalPropertiesKind.Forbidden, b.AdditionalProperties);
    }

    [Fact]
    public void Read_PropertyDictionaries_CannotBeMutated()
    {
        const string schema = """
            {
              "$defs": {
                "A": {"properties": {"self": {"$ref": "#/$defs/A"}}}
              },
              "properties": {
                "a": {"$ref": "#/$defs/A"},
                "array": {"type": "array", "items": {}}
              },
              "additionalProperties": {}
            }
            """;

        var root = Read(schema);
        var array = root.Properties["array"];
        Assert.NotNull(array.Items);
        Assert.NotNull(root.AdditionalPropertiesSchema);
        SchemaNode[] nodes = [root, root.Properties["a"], array, array.Items, root.AdditionalPropertiesSchema];

        foreach (var node in nodes)
        {
            var properties = Assert.IsType<IDictionary<string, SchemaNode>>(node.Properties, exactMatch: false);

            Assert.True(properties.IsReadOnly, "Expected the property dictionary to be read-only.");
            Assert.Throws<NotSupportedException>(() => properties.Add("unexpected", new SchemaNode()));
            Assert.Throws<NotSupportedException>(() => properties.Clear());

            if (properties.Count > 0)
            {
                var key = properties.Keys.First();
                Assert.Throws<NotSupportedException>(() => properties[key] = new SchemaNode());
            }
        }

        Assert.Same(root.Properties["a"], root.Properties["a"].Properties["self"]);
    }

    [Fact]
    public void Read_IgnoredKeywords_AreAccepted()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "t",
              "description": "d",
              "deprecated": true,
              "type": "object",
              "required": ["a"],
              "minProperties": 1,
              "maxProperties": 2,
              "properties": {
                "a": {"type": "integer", "minimum": 0, "maximum": 5, "exclusiveMinimum": 0, "enum": [1, 2]},
                "b": {"type": "array", "minItems": 1, "items": {"type": "string"}}
              }
            }
            """;

        var root = Read(schema);

        Assert.Equal(SchemaValueTypes.Integer, root.Properties["a"].ValueTypes);
        var items = root.Properties["b"].Items;
        Assert.NotNull(items);
        Assert.Equal(SchemaValueTypes.String, items.ValueTypes);
    }

    private static void AssertUnsupported(string schema, string? reason = null)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Read(schema));

        if (reason is null)
        {
            Assert.StartsWith("Unsupported configuration schema:", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal($"Unsupported configuration schema: {reason}", exception.Message);
        }
    }

    private static SchemaNode Read(string schema)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(schema));
        return ConfigurationSchemaReader.Read(stream);
    }
}
