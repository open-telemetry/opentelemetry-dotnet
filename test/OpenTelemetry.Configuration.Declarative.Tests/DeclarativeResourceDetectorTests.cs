// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeResourceDetectorTests
{
    [Fact]
    public void Detect_NoResourceSection_ReturnsEmpty()
    {
        var resource = Detect("""
            file_format: "1.0"
            """);

        Assert.Same(Resource.Empty, resource);
    }

    [Fact]
    public void Detect_ResourceSectionWithNoAttributes_ReturnsEmpty()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes_list: ""
            """);

        Assert.Same(Resource.Empty, resource);
    }

    [Fact]
    public void Detect_StringAttribute_FlowsAsString()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  type: string
                  value: "my-service"
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "service.name");
        Assert.Equal("my-service", Assert.IsType<string>(attr.Value));
    }

    [Fact]
    public void Detect_StringAttribute_DefaultType_FlowsAsString()
    {
        // The schema default type is string; omitting type should still work.
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: service.version
                  value: "2.0.0"
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "service.version");
        Assert.Equal("2.0.0", Assert.IsType<string>(attr.Value));
    }

    [Fact]
    public void Detect_BoolAttribute_True_FlowsAsBool()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: feature.enabled
                  type: bool
                  value: true
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "feature.enabled");
        Assert.True(Assert.IsType<bool>(attr.Value), "Expected feature.enabled to be true.");
    }

    [Fact]
    public void Detect_BoolAttribute_False_FlowsAsBool()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: feature.enabled
                  type: bool
                  value: false
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "feature.enabled");
        Assert.False(Assert.IsType<bool>(attr.Value), "Expected feature.enabled to be false.");
    }

    [Fact]
    public void Detect_IntAttribute_Decimal_FlowsAsLong()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: retry.count
                  type: int
                  value: 42
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "retry.count");
        Assert.Equal(42L, Assert.IsType<long>(attr.Value));
    }

    [Fact]
    public void Detect_IntAttribute_Negative_FlowsAsLong()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: retry.count
                  type: int
                  value: -7
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "retry.count");
        Assert.Equal(-7L, Assert.IsType<long>(attr.Value));
    }

    [Fact]
    public void Detect_IntAttribute_HexLiteral_FlowsAsLong()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: flags
                  type: int
                  value: 0x1F
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "flags");
        Assert.Equal(31L, Assert.IsType<long>(attr.Value));
    }

    [Fact]
    public void Detect_IntAttribute_OctalLiteral_FlowsAsLong()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: perms
                  type: int
                  value: 0o17
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "perms");
        Assert.Equal(15L, Assert.IsType<long>(attr.Value));
    }

    [Fact]
    public void Detect_IntAttribute_UnrepresentableInteger_IsSkipped()
    {
        // Values beyond long range are skipped; no other attributes are affected.
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: too.big
                  type: int
                  value: 99999999999999999999
                - name: service.name
                  value: "kept"
            """);

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "too.big");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "kept");
    }

    [Fact]
    public void Detect_UnrepresentableInteger_DoesNotSuppressLaterValidDuplicate()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: retry.count
                  type: int
                  value: 99999999999999999999
                - name: retry.count
                  value: fallback
            """);

        var attribute = Assert.Single(resource.Attributes, a => a.Key == "retry.count");
        Assert.Equal("fallback", Assert.IsType<string>(attribute.Value));
    }

    [Fact]
    public void Detect_DoubleAttribute_FloatLiteral_FlowsAsDouble()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: score
                  type: double
                  value: 3.14
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "score");
        Assert.Equal(3.14, Assert.IsType<double>(attr.Value), precision: 5);
    }

    [Fact]
    public void Detect_DoubleAttribute_IntegerLiteral_IsWidened()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: score
                  type: double
                  value: 1
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "score");
        Assert.Equal(1.0, Assert.IsType<double>(attr.Value));
    }

    [Fact]
    public void Detect_DoubleAttribute_Infinity_FlowsAsDouble()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: limit
                  type: double
                  value: .inf
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "limit");
        Assert.Equal(double.PositiveInfinity, Assert.IsType<double>(attr.Value));
    }

    [Fact]
    public void Detect_DoubleAttribute_NegativeInfinity_FlowsAsDouble()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: limit
                  type: double
                  value: -.inf
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "limit");
        Assert.Equal(double.NegativeInfinity, Assert.IsType<double>(attr.Value));
    }

    [Fact]
    public void Detect_DoubleAttribute_NaN_FlowsAsDouble()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: sentinel
                  type: double
                  value: .nan
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "sentinel");
        Assert.True(double.IsNaN(Assert.IsType<double>(attr.Value)));
    }

    [Fact]
    public void Detect_StringArrayAttribute_FlowsAsStringArray()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: regions
                  type: string_array
                  value: ["us-east-1", "eu-west-1"]
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "regions");
        var arr = Assert.IsType<string[]>(attr.Value);
        Assert.Equal(["us-east-1", "eu-west-1"], arr);
    }

    [Fact]
    public void Detect_StringArrayAttribute_SingleElement_FlowsAsStringArray()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: tags
                  type: string_array
                  value: ["only"]
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "tags");
        Assert.Equal(["only"], Assert.IsType<string[]>(attr.Value));
    }

    [Fact]
    public void Detect_BoolArrayAttribute_FlowsAsBoolArray()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: flags
                  type: bool_array
                  value: [true, false, true]
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "flags");
        Assert.Equal([true, false, true], Assert.IsType<bool[]>(attr.Value));
    }

    [Fact]
    public void Detect_IntArrayAttribute_FlowsAsLongArray()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: ports
                  type: int_array
                  value: [8080, 8443, 9090]
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "ports");
        Assert.Equal([8080L, 8443L, 9090L], Assert.IsType<long[]>(attr.Value));
    }

    [Fact]
    public void Detect_IntArrayAttribute_UnrepresentableItem_ArrayIsSkipped()
    {
        // If any item overflows, the whole array is skipped.
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: big.ports
                  type: int_array
                  value: [8080, 99999999999999999999]
                - name: service.name
                  value: "kept"
            """);

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "big.ports");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "kept");
    }

    [Fact]
    public void Detect_DoubleArrayAttribute_FlowsAsDoubleArray()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: scores
                  type: double_array
                  value: [1.1, 2.2, 3.3]
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "scores");
        var arr = Assert.IsType<double[]>(attr.Value);
        Assert.Equal(3, arr.Length);
        Assert.Equal(1.1, arr[0], precision: 5);
        Assert.Equal(2.2, arr[1], precision: 5);
        Assert.Equal(3.3, arr[2], precision: 5);
    }

    [Fact]
    public void Detect_DoubleArrayAttribute_IntegerItems_AreWidened()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: scores
                  type: double_array
                  value: [1, 2, 3]
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "scores");
        Assert.Equal([1.0, 2.0, 3.0], Assert.IsType<double[]>(attr.Value));
    }

    [Theory]
    [InlineData("", "144115188075855889", "0x200000000000011", "0o10000000000000000021", 144115188075855904.0)]
    [InlineData("!!float ", "144115188075855889", "0x200000000000011", "0o10000000000000000021", 144115188075855904.0)]
    [InlineData("", "36893488147419107584", "0x20000000000001100", "0o4000000000000000010400", 36893488147419111424.0)]
    [InlineData("!!float ", "36893488147419107584", "0x20000000000001100", "0o4000000000000000010400", 36893488147419111424.0)]
    public void Detect_DoubleAttributes_IntegerNotations_RoundConsistently(
        string tag,
        string decimalValue,
        string hexValue,
        string octalValue,
        double expected)
    {
        var resource = Detect($"""
            file_format: "1.0"
            resource:
              attributes:
                - name: decimal
                  type: double
                  value: {tag}{decimalValue}
                - name: hex
                  type: double
                  value: {tag}{hexValue}
                - name: octal
                  type: double
                  value: {tag}{octalValue}
                - name: array
                  type: double_array
                  value: [{tag}{decimalValue}, {tag}{hexValue}, {tag}{octalValue}]
            """);

        var attributes = resource.Attributes.ToDictionary(a => a.Key, a => a.Value);
        Assert.Equal(4, attributes.Count);
        Assert.Equal(expected, Assert.IsType<double>(attributes["decimal"]));
        Assert.Equal(expected, Assert.IsType<double>(attributes["hex"]));
        Assert.Equal(expected, Assert.IsType<double>(attributes["octal"]));
        Assert.Equal([expected, expected, expected], Assert.IsType<double[]>(attributes["array"]));
    }

    [Theory]
    [MemberData(nameof(YamlScalarConverterTests.DecimalFloatRoundingCases), MemberType = typeof(YamlScalarConverterTests))]
    public void Detect_DoubleAttributes_DecimalRounding_IsExact(string value, long expectedBits)
    {
        var resource = Detect($"""
            file_format: "1.0"
            resource:
              attributes:
                - name: scalar
                  type: double
                  value: {value}
                - name: array
                  type: double_array
                  value: [{value}, !!float {value}]
            """);

        var attributes = resource.Attributes.ToDictionary(a => a.Key, a => a.Value);
        Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(Assert.IsType<double>(attributes["scalar"])));
        var array = Assert.IsType<double[]>(attributes["array"]);
        Assert.Equal(2, array.Length);
        Assert.All(array, item => Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(item)));
    }

    [Fact]
    public void Detect_NullValue_AttributeIsSkipped()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: skipped
                  value: ~
                - name: kept
                  value: "present"
            """);

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "skipped");
        Assert.Contains(resource.Attributes, a => a.Key == "kept" && (string)a.Value == "present");
    }

    [Fact]
    public void Detect_DuplicateNames_FirstWins()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: "first"
                - name: service.name
                  value: "second"
            """);

        var attr = Assert.Single(resource.Attributes, a => a.Key == "service.name");
        Assert.Equal("first", Assert.IsType<string>(attr.Value));
    }

    [Fact]
    public void Detect_NonConformingName_AttributeStillEmitted()
    {
        // Names that don't pass the OTel attribute naming regex are warned about but not dropped.
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: "123-starts-with-digit"
                  value: "present"
            """);

        Assert.Contains(resource.Attributes, a => a.Key == "123-starts-with-digit" && (string)a.Value == "present");
    }

    [Fact]
    public void Detect_NameWithCommaOrEquals_IsAccepted()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: "key,with=special"
                  value: "ok"
            """);

        Assert.Contains(resource.Attributes, a => a.Key == "key,with=special" && (string)a.Value == "ok");
    }

    [Fact]
    public void Detect_SchemaUrlAbsent_ResourceSchemaUrlIsNull()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: "svc"
            """);

        Assert.Null(resource.SchemaUrl);
    }

    [Fact]
    public void Detect_SchemaUrlPresent_IsAppliedToResource()
    {
        var resource = BuildResource("""
            file_format: "1.0"
            resource:
              schema_url: "https://opentelemetry.io/schemas/1.24.0"
              attributes:
                - name: service.name
                  value: "svc"
            """);

        Assert.Equal("https://opentelemetry.io/schemas/1.24.0", resource.SchemaUrl);
    }

    [Fact]
    public void Detect_SchemaUrlOnlyNoAttributes_ReturnsResourceWithSchemaUrl()
    {
        var resource = BuildResource("""
            file_format: "1.0"
            resource:
              schema_url: "https://opentelemetry.io/schemas/1.24.0"
            """);

        Assert.Equal("https://opentelemetry.io/schemas/1.24.0", resource.SchemaUrl);
    }

    [Fact]
    public void Detect_SchemaUrlPresentNull_KeepsSchemaUrlFromOtherDetectors()
    {
        var resource = BuildResource(
            """
            file_format: "1.0"
            resource:
              schema_url: ~
            """,
            "https://opentelemetry.io/schemas/1.24.0");

        Assert.Equal("https://opentelemetry.io/schemas/1.24.0", resource.SchemaUrl);
    }

    [Fact]
    public void Detect_SchemaUrlMatchesOtherDetectors_IsRetained()
    {
        var resource = BuildResource(
            """
            file_format: "1.0"
            resource:
              schema_url: "https://opentelemetry.io/schemas/1.24.0"
            """,
            "https://opentelemetry.io/schemas/1.24.0");

        Assert.Equal("https://opentelemetry.io/schemas/1.24.0", resource.SchemaUrl);
    }

    [Fact]
    public void Detect_SchemaUrlConflictsWithOtherDetectors_ResourceSchemaUrlIsNull()
    {
        var resource = BuildResource(
            """
            file_format: "1.0"
            resource:
              schema_url: "https://opentelemetry.io/schemas/1.24.0"
              attributes:
                - name: service.name
                  value: "svc"
            """,
            "https://opentelemetry.io/schemas/1.44.0");

        Assert.Null(resource.SchemaUrl);
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "svc");
    }

    [Fact]
    public void Detect_MultipleAttributes_AllFlowWithCorrectTypes()
    {
        var resource = Detect("""
            file_format: "1.0"
            resource:
              attributes:
                - name: a.string
                  type: string
                  value: "hello"
                - name: a.bool
                  type: bool
                  value: true
                - name: a.int
                  type: int
                  value: 99
                - name: a.double
                  type: double
                  value: 1.5
            """);

        Assert.Contains(resource.Attributes, a => a.Key == "a.string" && (string)a.Value == "hello");
        Assert.Contains(resource.Attributes, a => a.Key == "a.bool" && (bool)a.Value);
        Assert.Contains(resource.Attributes, a => a.Key == "a.int" && (long)a.Value == 99L);
        Assert.Contains(resource.Attributes, a => a.Key == "a.double" && Math.Abs((double)a.Value - 1.5) < 1e-9);
    }

    private static Resource Detect(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var filePath = new FilePath(factory.CreateYamlFile(yaml));
        var accessor = new DeclarativeConfigurationDocumentAccessor(filePath);
        return new DeclarativeResourceDetector(accessor).Detect();
    }

    private static Resource BuildResource(string yaml, string? existingSchemaUrl = null)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var filePath = new FilePath(factory.CreateYamlFile(yaml));
        var accessor = new DeclarativeConfigurationDocumentAccessor(filePath);
        var resourceBuilder = ResourceBuilder.CreateEmpty();
        if (existingSchemaUrl != null)
        {
            resourceBuilder.AddDetector(new SchemaUrlResourceDetector(existingSchemaUrl));
        }

        resourceBuilder.AddDetector(new DeclarativeResourceDetector(accessor));
        return resourceBuilder.Build();
    }

    private sealed class SchemaUrlResourceDetector(string schemaUrl) : IResourceDetector
    {
        public Resource Detect() => new([], schemaUrl);
    }
}
