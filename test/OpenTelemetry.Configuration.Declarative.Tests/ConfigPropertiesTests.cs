// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ConfigPropertiesTests
{
    [Fact]
    public void GetString_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetString("x").Outcome);

    [Fact]
    public void GetBoolean_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetBoolean("x").Outcome);

    [Fact]
    public void GetLong_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetLong("x").Outcome);

    [Fact]
    public void GetDouble_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetDouble("x").Outcome);

    [Fact]
    public void GetInt_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetInt("x").Outcome);

    [Fact]
    public void GetMapping_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetMapping("x").Outcome);

    [Fact]
    public void GetMappingList_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetMappingList("x").Outcome);

    [Fact]
    public void GetStringList_Absent_ReturnsAbsent() =>
        Assert.Equal(ConfigValueOutcome.Absent, EmptyProperties().GetStringList("x").Outcome);

    [Fact]
    public void GetBooleanIntLongAndDoubleLists_Absent_ReturnAbsent()
    {
        var properties = EmptyProperties();

        Assert.Equal(ConfigValueOutcome.Absent, properties.GetBooleanList("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetIntList("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetLongList("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetDoubleList("x").Outcome);
    }

    [Fact]
    public void GetValueKind_Absent_ReturnsNull() =>
        Assert.Null(EmptyProperties().GetValueKind("x"));

    [Fact]
    public void GetValueKind_PresentNull_ReturnsNullKind() =>
        Assert.Equal(ConfigValueKind.Null, Build("k", ConfigValue.Null).GetValueKind("k"));

    [Fact]
    public void GetValueKind_TypeMismatch_ReturnsActualKind()
    {
        var properties = Build("k", ConfigValue.Boolean(true));

        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetString("k").Outcome);
        Assert.Equal(ConfigValueKind.Boolean, properties.GetValueKind("k"));
    }

    [Fact]
    public void GetValueKind_NumericConversion_ReturnsAuthoredKind()
    {
        var properties = Build("k", ConfigValue.Double(5.0));

        Assert.Equal(ConfigValueOutcome.Present, properties.GetLong("k").Outcome);
        Assert.Equal(ConfigValueKind.Double, properties.GetValueKind("k"));
    }

    [Fact]
    public void GetValueKind_UnrepresentableInteger_ReturnsIntegerKind() =>
        Assert.Equal(
            ConfigValueKind.Integer,
            Build("k", ConfigValue.UnrepresentableInteger()).GetValueKind("k"));

    [Fact]
    public void Getters_NullKey_ThrowArgumentNullException()
    {
        var properties = EmptyProperties();

        Assert.Throws<ArgumentNullException>(() => properties.GetValueKind(null!));
        Assert.Throws<ArgumentNullException>(() => properties.GetString(null!));
    }

    [Fact]
    public void GetString_PresentNull_ReturnsPresentNull()
    {
        var properties = Build("k", ConfigValue.Null);
        var result = properties.GetString("k");
        Assert.Equal(ConfigValueOutcome.PresentNull, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GetBoolean_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetBoolean("k").Outcome);

    [Fact]
    public void GetLong_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetLong("k").Outcome);

    [Fact]
    public void GetDouble_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetDouble("k").Outcome);

    [Fact]
    public void GetInt_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetInt("k").Outcome);

    [Fact]
    public void GetMapping_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetMapping("k").Outcome);

    [Fact]
    public void GetMappingList_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetMappingList("k").Outcome);

    [Fact]
    public void GetStringList_PresentNull_ReturnsPresentNull() =>
        Assert.Equal(ConfigValueOutcome.PresentNull, Build("k", ConfigValue.Null).GetStringList("k").Outcome);

    [Fact]
    public void GetBooleanIntLongAndDoubleLists_PresentNull_ReturnPresentNull()
    {
        var properties = Build("k", ConfigValue.Null);

        Assert.Equal(ConfigValueOutcome.PresentNull, properties.GetBooleanList("k").Outcome);
        Assert.Equal(ConfigValueOutcome.PresentNull, properties.GetIntList("k").Outcome);
        Assert.Equal(ConfigValueOutcome.PresentNull, properties.GetLongList("k").Outcome);
        Assert.Equal(ConfigValueOutcome.PresentNull, properties.GetDoubleList("k").Outcome);
    }

    [Fact]
    public void GetString_Present_ReturnsPresentWithValue()
    {
        var result = Build("k", ConfigValue.String("hello")).GetString("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void GetBoolean_Present_ReturnsPresentWithValue()
    {
        var result = Build("k", ConfigValue.Boolean(true)).GetBoolean("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.True(result.Value);
    }

    [Fact]
    public void GetLong_Present_ReturnsPresentWithValue()
    {
        var result = Build("k", ConfigValue.Integer(42L)).GetLong("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(42L, result.Value);
    }

    [Fact]
    public void GetDouble_Present_ReturnsPresentWithValue()
    {
        var result = Build("k", ConfigValue.Double(3.14)).GetDouble("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(3.14, result.Value);
    }

    [Fact]
    public void GetInt_Present_ReturnsPresentWithValue()
    {
        var result = Build("k", ConfigValue.Integer(7L)).GetInt("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void GetMapping_Present_ReturnsPresentWithValue()
    {
        var nested = Build("inner", ConfigValue.String("v"));
        var result = Build("k", ConfigValue.Mapping(nested)).GetMapping("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void GetMappingList_Present_ReturnsPresentWithList()
    {
        var nested = Build("x", ConfigValue.String("a"));
        var seq = ConfigValue.Sequence([ConfigValue.Mapping(nested)]);
        var result = Build("k", seq).GetMappingList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Single(result.Value!);
    }

    [Fact]
    public void GetStringList_Present_ReturnsPresentWithList()
    {
        var seq = ConfigValue.Sequence([ConfigValue.String("a"), ConfigValue.String("b")]);
        var result = Build("k", seq).GetStringList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal("a", v), v => Assert.Equal("b", v));
    }

    [Fact]
    public void GetString_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.Boolean(true)).GetString("k").Outcome);

    [Fact]
    public void GetBoolean_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("hello")).GetBoolean("k").Outcome);

    [Fact]
    public void GetLong_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("42")).GetLong("k").Outcome);

    [Fact]
    public void GetDouble_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("3.14")).GetDouble("k").Outcome);

    [Fact]
    public void GetInt_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("7")).GetInt("k").Outcome);

    [Fact]
    public void GetMapping_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("x")).GetMapping("k").Outcome);

    [Fact]
    public void GetMappingList_WrongKind_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("x")).GetMappingList("k").Outcome);

    [Fact]
    public void GetStringList_WrongKind_ReturnsTypeMismatch()
    {
        var nested = Build("x", ConfigValue.String("v"));
        var seq = ConfigValue.Sequence([ConfigValue.Mapping(nested)]);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", seq).GetStringList("k").Outcome);
    }

    [Fact]
    public void StringKind_TrueText_IsNotBoolean()
    {
        // "true" as a String-kind value must NOT be readable as bool (quoting forces string).
        var properties = Build("k", ConfigValue.String("true"));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetBoolean("k").Outcome);
    }

    [Fact]
    public void BooleanKind_IsNotString()
    {
        var properties = Build("k", ConfigValue.Boolean(true));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetString("k").Outcome);
    }

    [Fact]
    public void IntegerOutsideIntRange_IsMismatchForGetInt()
    {
        // long.MaxValue > int.MaxValue - mismatch for GetInt, value for GetLong
        var properties = Build("k", ConfigValue.Integer(long.MaxValue));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetInt("k").Outcome);
        Assert.Equal(ConfigValueOutcome.Present, properties.GetLong("k").Outcome);
        Assert.Equal(long.MaxValue, properties.GetLong("k").Value);
    }

    [Fact]
    public void Integer_ReadsAsDouble()
    {
        var properties = Build("k", ConfigValue.Integer(5L));
        var result = properties.GetDouble("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(5.0, result.Value);
    }

    [Fact]
    public void IntegerNotExactlyRepresentableAsDouble_UsesClrWidening()
    {
        var properties = Build("k", ConfigValue.Integer(9007199254740993L));
        var result = properties.GetDouble("k");

        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(9007199254740992D, result.Value);
    }

    [Fact]
    public void IntegerExactlyRepresentableAsDouble_IsReadable()
    {
        var properties = Build("k", ConfigValue.Integer(9007199254740992L));
        var result = properties.GetDouble("k");

        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(9007199254740992.0, result.Value);
    }

    [Fact]
    public void Double_IntegralValue_ReadsAsLong()
    {
        var properties = Build("k", ConfigValue.Double(5.0));
        var result = properties.GetLong("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(5L, result.Value);
    }

    [Fact]
    public void Double_IntegralValue_ReadsAsInt()
    {
        var properties = Build("k", ConfigValue.Double(5.0));
        var result = properties.GetInt("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Double_FractionalValue_IsMismatchForLong()
    {
        var properties = Build("k", ConfigValue.Double(5.7));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetLong("k").Outcome);
    }

    [Fact]
    public void Double_FractionalValue_IsMismatchForInt()
    {
        var properties = Build("k", ConfigValue.Double(5.7));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetInt("k").Outcome);
    }

    [Fact]
    public void Double_OutsideLongRange_IsMismatchForLong()
    {
        // 2^63 is one past long.MaxValue, not representable as long.
        var properties = Build("k", ConfigValue.Double(9.3e18));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetLong("k").Outcome);
    }

    [Fact]
    public void UnrepresentableInteger_IsMismatchForLong()
    {
        var properties = Build("k", ConfigValue.UnrepresentableInteger());
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetLong("k").Outcome);
    }

    [Fact]
    public void UnrepresentableInteger_IsMismatchForDouble()
    {
        var properties = Build("k", ConfigValue.UnrepresentableInteger());
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetDouble("k").Outcome);
    }

    [Fact]
    public void UnrepresentableInteger_IsMismatchForInt()
    {
        var properties = Build("k", ConfigValue.UnrepresentableInteger());
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetInt("k").Outcome);
    }

    [Fact]
    public void UnrepresentableInteger_StillAppearsInKeys()
    {
        var properties = Build("k", ConfigValue.UnrepresentableInteger());
        Assert.Contains("k", properties.Keys);
    }

    [Fact]
    public void UnrepresentableInteger_RetainsIntegerKind()
    {
        var value = ConfigValue.UnrepresentableInteger();
        Assert.Equal(ConfigValueKind.Integer, value.Kind);
        Assert.True(value.IsUnrepresentable);
        var ex = Assert.Throws<InvalidOperationException>(() => value.AsLong());
        Assert.Equal("Cannot read an out-of-range integer value as long.", ex.Message);
    }

    [Fact]
    public void Null_EqualsDefaultAndHasNullKind()
    {
        Assert.Equal(default, ConfigValue.Null);
        Assert.Equal(ConfigValueKind.Null, ConfigValue.Null.Kind);
        Assert.False(ConfigValue.Null.IsUnrepresentable);
    }

    [Fact]
    public void Double_PositiveInfinity_IsReadableAsDouble()
    {
        var properties = Build("k", ConfigValue.Double(double.PositiveInfinity));
        var result = properties.GetDouble("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(double.PositiveInfinity, result.Value);
    }

    [Fact]
    public void Double_PositiveInfinity_IsMismatchForLong()
    {
        var properties = Build("k", ConfigValue.Double(double.PositiveInfinity));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetLong("k").Outcome);
    }

    [Fact]
    public void Integer_One_ReadsAsDouble()
    {
        var properties = Build("ratio", ConfigValue.Integer(1L));
        var result = properties.GetDouble("ratio");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(1.0, result.Value);
    }

    [Fact]
    public void Double_FivePointZero_ReadsAsLong()
    {
        var properties = Build("timeout", ConfigValue.Double(5.0));
        var result = properties.GetLong("timeout");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(5L, result.Value);
    }

    [Fact]
    public void Double_ExactLongMinValue_ReadsAsLong()
    {
        // -2^63 is exactly representable as double; the boundary check must admit it.
        var properties = Build("k", ConfigValue.Double(-9223372036854775808.0));
        var result = properties.GetLong("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(long.MinValue, result.Value);
    }

    [Fact]
    public void Double_LongMaxValueAsDouble_IsMismatchForLong()
    {
        // (double)long.MaxValue rounds up to 2^63, which is one past long.MaxValue and must be rejected.
        var properties = Build("k", ConfigValue.Double(long.MaxValue));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetLong("k").Outcome);
    }

    [Fact]
    public void NestedProperties_ThreeLevels_AreReadable()
    {
        var level3 = Build("leaf", ConfigValue.String("deep"));
        var level2 = Build("l3", ConfigValue.Mapping(level3));
        var level1 = Build("l2", ConfigValue.Mapping(level2));

        var r2 = level1.GetMapping("l2");
        Assert.Equal(ConfigValueOutcome.Present, r2.Outcome);

        var r3 = r2.Value!.GetMapping("l3");
        Assert.Equal(ConfigValueOutcome.Present, r3.Outcome);

        var leaf = r3.Value!.GetString("leaf");
        Assert.Equal(ConfigValueOutcome.Present, leaf.Outcome);
        Assert.Equal("deep", leaf.Value);
    }

    [Fact]
    public void SequenceOfMappings_ReturnsAllMappings()
    {
        var m1 = Build("a", ConfigValue.String("1"));
        var m2 = Build("a", ConfigValue.String("2"));
        var seq = ConfigValue.Sequence([ConfigValue.Mapping(m1), ConfigValue.Mapping(m2)]);
        var properties = Build("items", seq);

        var result = properties.GetMappingList("items");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("1", result.Value[0].GetString("a").Value);
        Assert.Equal("2", result.Value[1].GetString("a").Value);
    }

    [Fact]
    public void SequenceOfScalars_ReturnsAllValues()
    {
        var seq = ConfigValue.Sequence([ConfigValue.String("x"), ConfigValue.String("y")]);
        var result = Build("k", seq).GetStringList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal("x", v), v => Assert.Equal("y", v));
    }

    [Fact]
    public void EmptySequence_ReturnsEmptyList_MappingList()
    {
        var seq = ConfigValue.Sequence([]);
        var result = Build("k", seq).GetMappingList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void EmptySequence_ReturnsEmptyList_StringList()
    {
        var seq = ConfigValue.Sequence([]);
        var result = Build("k", seq).GetStringList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void EmptySequence_ReturnsEmptyList_BooleanList()
    {
        var seq = ConfigValue.Sequence([]);
        var result = Build("k", seq).GetBooleanList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void EmptySequence_ReturnsEmptyList_IntList()
    {
        var seq = ConfigValue.Sequence([]);
        var result = Build("k", seq).GetIntList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void EmptySequence_ReturnsEmptyList_LongList()
    {
        var seq = ConfigValue.Sequence([]);
        var result = Build("k", seq).GetLongList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void EmptySequence_ReturnsEmptyList_DoubleList()
    {
        var seq = ConfigValue.Sequence([]);
        var result = Build("k", seq).GetDoubleList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void EmptyMapping_ReturnsEmptyProperties()
    {
        var nested = new ConfigPropertiesBuilder().Build();
        var properties = Build("k", ConfigValue.Mapping(nested));
        var result = properties.GetMapping("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!.Keys);
    }

    [Fact]
    public void MixedSequence_IsMismatchForMappingList()
    {
        // Sequence contains a scalar and a mapping - not a valid mapping sequence.
        var seq = ConfigValue.Sequence(
        [
            ConfigValue.Mapping(Build("x", ConfigValue.String("v"))),
            ConfigValue.String("not-a-mapping"),
        ]);
        var properties = Build("k", seq);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetMappingList("k").Outcome);
        Assert.Contains("k", properties.Keys); // key is retained despite mismatch
    }

    [Fact]
    public void MixedSequence_IsMismatchForStringList()
    {
        // Sequence contains a string and a mapping - not a valid scalar sequence.
        var seq = ConfigValue.Sequence(
        [
            ConfigValue.String("ok"),
            ConfigValue.Mapping(Build("x", ConfigValue.String("v"))),
        ]);
        var properties = Build("k", seq);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetStringList("k").Outcome);
        Assert.Contains("k", properties.Keys); // key is retained despite mismatch
    }

    [Fact]
    public void GetMapping_NullValue_ReturnsPresentNull_AggregationDropCase()
    {
        // The spec's MUST example: drop: (present-null) must select the drop aggregation.
        // GetMapping on a null key must return PresentNull, not Absent, not a null ConfigProperties.
        var properties = Build("drop", ConfigValue.Null);
        var result = properties.GetMapping("drop");
        Assert.Equal(ConfigValueOutcome.PresentNull, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GetMappingList_NullValue_ReturnsPresentNull()
    {
        var properties = Build("k", ConfigValue.Null);
        var result = properties.GetMappingList("k");
        Assert.Equal(ConfigValueOutcome.PresentNull, result.Outcome);
    }

    [Fact]
    public void GetStringList_NullElement_ReturnsTypeMismatch()
    {
        // A null element is not a scalar of T, so the whole sequence mismatches - the same rule
        // GetMappingList applies. It holds for every element type, including the reference type:
        // present-null is an outcome for a property, not for an element within one.
        var seq = ConfigValue.Sequence([ConfigValue.String("a"), ConfigValue.Null, ConfigValue.String("b")]);
        var properties = Build("k", seq);
        var result = properties.GetStringList("k");
        Assert.Equal(ConfigValueOutcome.TypeMismatch, result.Outcome);
        Assert.Null(result.Value);
        Assert.Contains("k", properties.Keys); // key is retained despite mismatch
    }

    [Fact]
    public void GetBooleanList_NullElement_ReturnsTypeMismatch()
    {
        // An unconstrained T? is a nullable annotation only, so before this rule the null element was
        // added as default(T) and the caller received Present with a fabricated false.
        var seq = ConfigValue.Sequence([ConfigValue.Boolean(true), ConfigValue.Null]);
        var result = Build("k", seq).GetBooleanList("k");
        Assert.Equal(ConfigValueOutcome.TypeMismatch, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GetLongList_NullElement_ReturnsTypeMismatch()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Integer(1L), ConfigValue.Null]);
        var result = Build("k", seq).GetLongList("k");
        Assert.Equal(ConfigValueOutcome.TypeMismatch, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GetDoubleList_NullElement_ReturnsTypeMismatch()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Double(1.5), ConfigValue.Null]);
        var result = Build("k", seq).GetDoubleList("k");
        Assert.Equal(ConfigValueOutcome.TypeMismatch, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GetIntList_NullElement_ReturnsTypeMismatch()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Integer(1L), ConfigValue.Null]);
        var result = Build("k", seq).GetIntList("k");
        Assert.Equal(ConfigValueOutcome.TypeMismatch, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GetLongList_AllNullElements_ReturnsTypeMismatch()
    {
        // Java's accessor removes null and mismatched elements and reports the remainder; a sequence of
        // nothing but nulls must not read as an empty list here, which would hide the data error.
        var seq = ConfigValue.Sequence([ConfigValue.Null, ConfigValue.Null]);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", seq).GetLongList("k").Outcome);
    }

    [Fact]
    public void MappingList_NullElement_IsMismatch()
    {
        // IReadOnlyList<ConfigProperties> has no slot for null; the whole sequence mismatches.
        // The typed list accessors apply the same rule to their elements.
        var seq = ConfigValue.Sequence(
        [
            ConfigValue.Mapping(Build("x", ConfigValue.String("v"))),
            ConfigValue.Null,
        ]);
        var properties = Build("k", seq);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetMappingList("k").Outcome);
        Assert.Contains("k", properties.Keys); // key is retained despite mismatch
    }

    [Fact]
    public void GetLongList_IntegerElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Integer(1L), ConfigValue.Integer(2L)]);
        var result = Build("k", seq).GetLongList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal(1L, v), v => Assert.Equal(2L, v));
    }

    [Fact]
    public void GetDoubleList_DoubleElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Double(1.1), ConfigValue.Double(2.2)]);
        var result = Build("k", seq).GetDoubleList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public void GetBooleanList_BooleanElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Boolean(true), ConfigValue.Boolean(false)]);
        var result = Build("k", seq).GetBooleanList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, Assert.True, Assert.False);
    }

    [Fact]
    public void GetIntList_InRangeIntegerElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Integer(10L), ConfigValue.Integer(20L)]);
        var result = Build("k", seq).GetIntList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal(10, v), v => Assert.Equal(20, v));
    }

    [Fact]
    public void GetLongAndStringLists_Present_ElementTypesAreNotNullable()
    {
        // The signature is IReadOnlyList<T>, not IReadOnlyList<T?>. With the null-element rule in place
        // the annotation is accurate for a value element type as well as a reference one, which it was
        // not while an unconstrained T? claimed to carry per-element nulls.
        var longs = Build("k", ConfigValue.Sequence([ConfigValue.Integer(1L)])).GetLongList("k").Value!;
        var strings = Build("k", ConfigValue.Sequence([ConfigValue.String("a")])).GetStringList("k").Value!;
        Assert.Equal(1L, longs[0]);
        Assert.Equal("a", strings[0]);
    }

    [Fact]
    public void GetStringList_NonSequenceValue_ReturnsTypeMismatch() =>
        Assert.Equal(ConfigValueOutcome.TypeMismatch, Build("k", ConfigValue.String("x")).GetStringList("k").Outcome);

    [Fact]
    public void GetBooleanIntLongAndDoubleLists_NonSequenceValue_ReturnTypeMismatch()
    {
        var properties = Build("k", ConfigValue.String("x"));

        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetBooleanList("k").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetIntList("k").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetLongList("k").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, properties.GetDoubleList("k").Outcome);
    }

    [Fact]
    public void GetLongList_IntegralDoubleElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Double(2.0), ConfigValue.Double(10.0)]);
        var result = Build("k", seq).GetLongList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal(2L, v), v => Assert.Equal(10L, v));
    }

    [Fact]
    public void GetDoubleList_IntegerElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Integer(3L), ConfigValue.Integer(7L)]);
        var result = Build("k", seq).GetDoubleList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal(3.0, v), v => Assert.Equal(7.0, v));
    }

    [Fact]
    public void GetDoubleList_InexactIntegerElement_UsesClrWidening()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Integer(9007199254740993L)]);
        var result = Build("k", seq).GetDoubleList("k");

        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(9007199254740992D, Assert.Single(result.Value!));
    }

    [Fact]
    public void GetIntList_IntegralDoubleElements_AreReadable()
    {
        var seq = ConfigValue.Sequence([ConfigValue.Double(4.0), ConfigValue.Double(9.0)]);
        var result = Build("k", seq).GetIntList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal(4, v), v => Assert.Equal(9, v));
    }

    [Fact]
    public void GetIntAndLongLists_UnrepresentableOrOutOfRangeElement_ReturnTypeMismatch()
    {
        Assert.Equal(
            ConfigValueOutcome.TypeMismatch,
            Build("k", ConfigValue.Sequence([ConfigValue.UnrepresentableInteger()])).GetLongList("k").Outcome);
        Assert.Equal(
            ConfigValueOutcome.TypeMismatch,
            Build("k", ConfigValue.Sequence([ConfigValue.Integer(long.MaxValue)])).GetIntList("k").Outcome);
        Assert.Equal(
            ConfigValueOutcome.TypeMismatch,
            Build("k", ConfigValue.Sequence([ConfigValue.Double(1.5)])).GetIntList("k").Outcome);
        Assert.Equal(
            ConfigValueOutcome.TypeMismatch,
            Build("k", ConfigValue.Sequence([ConfigValue.Double(1.5)])).GetLongList("k").Outcome);
    }

    [Fact]
    public void GetMappingList_SingleMapping_ReturnsTypeMismatch()
    {
        var nested = Build("nested", ConfigValue.String("value"));

        Assert.Equal(
            ConfigValueOutcome.TypeMismatch,
            Build("k", ConfigValue.Mapping(nested)).GetMappingList("k").Outcome);
    }

    [Fact]
    public void Keys_IncludesNullValuedKeys()
    {
        var properties = new ConfigPropertiesBuilder()
            .Add("present", ConfigValue.String("v"))
            .Add("nulled", ConfigValue.Null)
            .Build();
        var keys = properties.Keys.ToList();
        Assert.Contains("present", keys);
        Assert.Contains("nulled", keys);
    }

    [Fact]
    public void Keys_ExcludesAbsentKeys()
    {
        var properties = Build("present", ConfigValue.String("v"));
        Assert.DoesNotContain("absent", properties.Keys);
    }

    [Fact]
    public void Keys_OrdinalComparison_CaseSensitive()
    {
        var properties = new ConfigPropertiesBuilder()
            .Add("Key", ConfigValue.String("upper"))
            .Add("key", ConfigValue.String("lower"))
            .Build();
        var keys = properties.Keys.ToList();
        Assert.Contains("Key", keys);
        Assert.Contains("key", keys);
        Assert.Equal(2, keys.Count);

        // Ordinal: "Key" and "key" are distinct
        Assert.Equal("upper", properties.GetString("Key").Value);
        Assert.Equal("lower", properties.GetString("key").Value);
    }

    [Fact]
    public void Keys_RejectsMutation()
    {
        var properties = Build("k", ConfigValue.String("v"));
        var keys = properties.Keys;
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)keys).Add("injected"));
    }

    [Fact]
    public void Builder_Add_DuplicateKey_Throws()
    {
        var builder = new ConfigPropertiesBuilder().Add("k", ConfigValue.String("v1"));
        Assert.Throws<ArgumentException>(() => builder.Add("k", ConfigValue.String("v2")));
    }

    [Fact]
    public void Builder_Add_NullKey_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ConfigPropertiesBuilder().Add(null!, ConfigValue.String("v")));

    [Fact]
    public void Builder_PublicTypedMethods_CreateReadableProperties()
    {
        var nested = new ConfigPropertiesBuilder().Add("child", "value").Build();
        var scalarValues = new[] { "one", "two" };
        var mappingValues = new[] { nested };
        var properties = new ConfigPropertiesBuilder()
            .AddNull("null")
            .Add("string", "value")
            .Add("boolean", true)
            .Add("int", 1)
            .Add("long", 2L)
            .Add("double", 0.25)
            .Add("mapping", nested)
            .AddScalarList("scalars", scalarValues)
            .AddPropertiesList("mappings", mappingValues)
            .Build();

        Assert.Equal(ConfigValueOutcome.PresentNull, properties.GetString("null").Outcome);
        Assert.Equal("value", properties.GetString("string").Value);
        Assert.True(properties.GetBoolean("boolean").Value);
        Assert.Equal(1, properties.GetInt("int").Value);
        Assert.Equal(2L, properties.GetLong("long").Value);
        Assert.Equal(0.25, properties.GetDouble("double").Value);
        Assert.Same(nested, properties.GetMapping("mapping").Value);
        Assert.Equal(scalarValues, properties.GetStringList("scalars").Value);
        Assert.Same(nested, Assert.Single(properties.GetMappingList("mappings").Value!));
    }

    [Fact]
    public void Builder_AddScalarList_SupportsAllScalarTypes()
    {
        var booleans = new[] { true, false };
        var ints = new[] { 1, 2, 0, int.MinValue, int.MaxValue };
        var longs = new[] { 3L, 4L, 0L, long.MinValue, long.MaxValue };
        var doubles = new[]
        {
            0.25,
            0.5,
            0.0,
            -0.0,
            double.MinValue,
            double.MaxValue,
            double.Epsilon,
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
        };
        var properties = new ConfigPropertiesBuilder()
            .AddScalarList("booleans", booleans)
            .AddScalarList("ints", ints)
            .AddScalarList("longs", longs)
            .AddScalarList("doubles", doubles)
            .Build();

        Assert.Equal(booleans, properties.GetBooleanList("booleans").Value);
        Assert.Equal(ints, properties.GetIntList("ints").Value);
        Assert.Equal(longs, properties.GetLongList("longs").Value);
        Assert.Equal(doubles, properties.GetDoubleList("doubles").Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Builder_Add_Int_RoundTripsBoundaryValues(int value)
    {
        var properties = new ConfigPropertiesBuilder().Add("k", value).Build();

        Assert.Equal(value, properties.GetInt("k").Value);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Builder_Add_Long_RoundTripsBoundaryValues(long value)
    {
        var properties = new ConfigPropertiesBuilder().Add("k", value).Build();

        Assert.Equal(value, properties.GetLong("k").Value);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Builder_Add_Double_RoundTripsBoundaryValues(double value)
    {
        var properties = new ConfigPropertiesBuilder().Add("k", value).Build();

        Assert.Equal(value, properties.GetDouble("k").Value);
    }

    [Fact]
    public void Builder_AddScalarList_UnsupportedType_Throws()
    {
        var dates = new[] { default(DateTime) };
        var exception = Assert.Throws<NotSupportedException>(
            () => new ConfigPropertiesBuilder().AddScalarList("dates", dates));

        Assert.Contains(nameof(DateTime), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderMutatedAfterBuild_DoesNotAffectBuiltProperties()
    {
        var builder = new ConfigPropertiesBuilder()
            .Add("k", ConfigValue.String("original"));

        var properties = builder.Build();

        // Mutation after build must not affect already-built ConfigProperties.
        // (A second Add on the same key would throw; use a fresh builder call via a new key.)
        builder.Add("new", ConfigValue.String("added"));

        Assert.Equal("original", properties.GetString("k").Value);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetString("new").Outcome);
    }

    [Fact]
    public void BuildCanBeCalledMultipleTimes_EachInstanceIsIndependent()
    {
        var builder = new ConfigPropertiesBuilder()
            .Add("k", ConfigValue.String("v1"));

        var properties1 = builder.Build();
        var properties2 = builder.Build();

        Assert.Equal("v1", properties1.GetString("k").Value);
        Assert.Equal("v1", properties2.GetString("k").Value);
        Assert.NotSame(properties1, properties2);
    }

    [Fact]
    public void ConfigValue_String_NullArgument_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ConfigValue.String(null!));

    [Fact]
    public void ConfigValue_Mapping_NullArgument_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ConfigValue.Mapping(null!));

    [Fact]
    public void ConfigValue_Sequence_NullArgument_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ConfigValue.Sequence(null!));

    [Fact]
    public void SequenceListMutatedAfterAdd_DoesNotAffectBuiltProperties()
    {
        var items = new List<ConfigValue> { ConfigValue.String("a") };
        var properties = new ConfigPropertiesBuilder()
            .Add("k", ConfigValue.Sequence(items))
            .Build();

        items.Add(ConfigValue.String("b"));
        items[0] = ConfigValue.String("mutated");

        var result = properties.GetStringList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);

        var expected = Assert.Single(result.Value!);
        Assert.Equal("a", expected);
    }

    [Fact]
    public void SequenceArrayMutatedAfterConstruction_DoesNotAffectBuiltProperties()
    {
        var items = new[] { ConfigValue.String("a"), ConfigValue.String("b") };
        var properties = Build("k", ConfigValue.Sequence(items));

        items[0] = ConfigValue.String("mutated");
        items[1] = ConfigValue.String("also-mutated");

        var result = properties.GetStringList("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Collection(result.Value!, v => Assert.Equal("a", v), v => Assert.Equal("b", v));
    }

    [Fact]
    public void SequenceOfMappings_SourceListMutatedAfterAdd_DoesNotAffectBuiltProperties()
    {
        var m1 = Build("a", ConfigValue.String("1"));
        var m2 = Build("a", ConfigValue.String("2"));
        var items = new List<ConfigValue> { ConfigValue.Mapping(m1), ConfigValue.Mapping(m2) };
        var properties = Build("items", ConfigValue.Sequence(items));

        items.Clear();
        items.Add(ConfigValue.Mapping(Build("a", ConfigValue.String("replaced"))));

        var result = properties.GetMappingList("items");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("1", result.Value[0].GetString("a").Value);
        Assert.Equal("2", result.Value[1].GetString("a").Value);
    }

    [Fact]
    public void Sequence_AsSequence_RejectsMutation()
    {
        var seq = ConfigValue.Sequence([ConfigValue.String("a")]);
        var list = seq.AsSequence();

        Assert.Throws<NotSupportedException>(() => ((IList<ConfigValue>)list).Add(ConfigValue.String("b")));
        Assert.Throws<NotSupportedException>(() => ((IList<ConfigValue>)list)[0] = ConfigValue.String("mutated"));
        Assert.Equal("a", list[0].AsString());
    }

    [Fact]
    public void MappingList_ReturnedList_RejectsMutation()
    {
        var nested = Build("x", ConfigValue.String("v"));
        var properties = Build("k", ConfigValue.Sequence([ConfigValue.Mapping(nested)]));
        var list = properties.GetMappingList("k").Value!;

        Assert.Throws<NotSupportedException>(() => ((IList<ConfigProperties>)list).Clear());
        Assert.Single(list);
    }

    [Fact]
    public void GetStringList_ReturnedList_RejectsMutation()
    {
        var properties = Build("k", ConfigValue.Sequence([ConfigValue.String("a")]));
        var list = properties.GetStringList("k").Value!;

        Assert.Throws<NotSupportedException>(() => ((IList<string>)list).Add("b"));

        var expected = Assert.Single(list);
        Assert.Equal("a", expected);
    }

    [Fact]
    public void Create_CopiesSourceDictionary()
    {
        var source = new Dictionary<string, ConfigValue>(StringComparer.Ordinal)
        {
            ["k"] = ConfigValue.String("original"),
        };

        var properties = ConfigProperties.Create(source);
        source["k"] = ConfigValue.String("mutated");
        source["added"] = ConfigValue.String("new");

        Assert.Equal("original", properties.GetString("k").Value);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetString("added").Outcome);
    }

    [Fact]
    public void Empty_AllKeysAbsent()
    {
        var properties = ConfigProperties.Empty;
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetString("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetBoolean("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetLong("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetDouble("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetInt("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetMapping("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetMappingList("x").Outcome);
        Assert.Equal(ConfigValueOutcome.Absent, properties.GetStringList("x").Outcome);
    }

    [Fact]
    public void Empty_Keys_ReturnsEmpty() =>
        Assert.Empty(ConfigProperties.Empty.Keys);

    [Fact]
    public void Empty_IsSafeToPassAsNestedMapping()
    {
        var properties = Build("k", ConfigValue.Mapping(ConfigProperties.Empty));
        var result = properties.GetMapping("k");
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        Assert.Empty(result.Value!.Keys);
    }

    [Fact]
    public void Empty_IsSameInstance() => Assert.Same(ConfigProperties.Empty, ConfigProperties.Empty);

    [Fact]
    public void ConfigValueResult_Deconstruct_Works()
    {
        var properties = Build("k", ConfigValue.String("hello"));
        var (outcome, value) = properties.GetString("k");
        Assert.Equal(ConfigValueOutcome.Present, outcome);
        Assert.Equal("hello", value);
    }

    [Fact]
    public void ConfigValueResult_Deconstruct_Absent_Works()
    {
        var (outcome, value) = EmptyProperties().GetString("missing");
        Assert.Equal(ConfigValueOutcome.Absent, outcome);
        Assert.Null(value);
    }

    [Fact]
    public void ConfigValueResult_TryGetValue_PresentReferenceValue_Works()
    {
        var properties = Build("k", ConfigValue.Mapping(Build("nested", ConfigValue.String("value"))));

        Assert.True(properties.GetMapping("k").TryGetValue(out var nested));
        Assert.Equal("value", nested.GetString("nested").Value);
    }

    [Fact]
    public void ConfigValueResult_TryGetValue_PresentValueType_Works()
    {
        var properties = Build("k", ConfigValue.Integer(42));

        Assert.True(properties.GetInt("k").TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void ConfigValueResult_TryGetValue_PresentFalseBoolean_ReturnsTrue()
    {
        var result = Build("k", ConfigValue.Boolean(false)).GetBoolean("k");

        Assert.True(result.TryGetValue(out var value));
        Assert.False(value);
    }

    [Fact]
    public void ConfigValueResult_TryGetValue_NonPresentOutcomes_ReturnFalse()
    {
        var properties = new ConfigPropertiesBuilder()
            .Add("null", ConfigValue.Null)
            .Add("mismatch", ConfigValue.Boolean(true))
            .Build();

        Assert.False(properties.GetString("missing").TryGetValue(out var absent));
        Assert.Null(absent);
        Assert.False(properties.GetString("null").TryGetValue(out var presentNull));
        Assert.Null(presentNull);
        Assert.False(properties.GetString("mismatch").TryGetValue(out var typeMismatch));
        Assert.Null(typeMismatch);
    }

    [Fact]
    public void ConfigValueResult_Position_ReflectsReadProperty()
    {
        var present = Build("present", ConfigValue.String("value").WithPosition(new(2, 4))).GetString("present");
        var presentNull = Build("null", ConfigValue.Null.WithPosition(new(3, 5))).GetString("null");
        var typeMismatch = Build("mismatch", ConfigValue.Boolean(true).WithPosition(new(7, 2))).GetString("mismatch");
        var absent = EmptyProperties().GetString("absent");

        Assert.Equal(ConfigValueOutcome.Present, present.Outcome);
        Assert.Equal(2, present.Position.Line);
        Assert.Equal(4, present.Position.Column);

        Assert.Equal(ConfigValueOutcome.PresentNull, presentNull.Outcome);
        Assert.Equal(3, presentNull.Position.Line);
        Assert.Equal(5, presentNull.Position.Column);

        Assert.Equal(ConfigValueOutcome.TypeMismatch, typeMismatch.Outcome);
        Assert.Equal(7, typeMismatch.Position.Line);
        Assert.Equal(2, typeMismatch.Position.Column);

        Assert.Equal(ConfigValueOutcome.Absent, absent.Outcome);
        Assert.Equal(ConfigValuePosition.Unknown, absent.Position);
        Assert.False(absent.Position.HasPosition);
    }

    private static ConfigProperties EmptyProperties() => ConfigProperties.Empty;

    private static ConfigProperties Build(string key, ConfigValue value)
        => new ConfigPropertiesBuilder().Add(key, value).Build();
}
