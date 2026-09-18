// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An immutable, typed view over a configuration mapping node.
/// </summary>
/// <remarks>
/// Keys use ordinal, case-sensitive comparison. Instances are safe for concurrent reads.
/// </remarks>
public sealed class ConfigProperties
{
    private readonly Dictionary<string, ConfigValue> values;

    private ConfigProperties(Dictionary<string, ConfigValue> values)
    {
        this.values = values;
    }

    /// <summary>
    /// Gets a shared empty <see cref="ConfigProperties"/> with no keys.
    /// </summary>
    public static ConfigProperties Empty { get; } =
        new(new Dictionary<string, ConfigValue>(0, StringComparer.Ordinal));

    /// <summary>
    /// Gets all keys present in this mapping.
    /// </summary>
    /// <remarks>The returned collection is read-only.</remarks>
    public IReadOnlyCollection<string> Keys => this.values.Keys;

    /// <summary>
    /// Gets the kind of value associated with <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// This method distinguishes an absent property, which returns <see langword="null"/>, from a
    /// property whose value is explicitly null, which returns <see cref="ConfigValueKind.Null"/>.
    /// It is primarily intended for reporting the actual kind after a typed read returns
    /// <see cref="ConfigValueOutcome.TypeMismatch"/>.
    /// </remarks>
    /// <param name="key">The key to inspect.</param>
    /// <returns>The value kind, or <see langword="null"/> when the key is absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueKind? GetValueKind(string key)
        => this.TryGetValue(key, out var value) ? value.Kind : null;

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="string"/> wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <returns>
    /// A result with outcome <see cref="ConfigValueOutcome.Absent"/>, <see cref="ConfigValueOutcome.PresentNull"/>,
    /// <see cref="ConfigValueOutcome.Present"/>, or <see cref="ConfigValueOutcome.TypeMismatch"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<string> GetString(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.String => new(ConfigValueOutcome.Present, value.AsString(), value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Double or
            ConfigValueKind.Integer or
            ConfigValueKind.Mapping or
            ConfigValueKind.Sequence or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="bool"/> wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<bool> GetBoolean(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Boolean => new(ConfigValueOutcome.Present, value.AsBoolean(), value.Position),
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Double or
            ConfigValueKind.Integer or
            ConfigValueKind.Mapping or
            ConfigValueKind.Sequence or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as an <see cref="int"/> wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Integer values are accepted when they fit in an <see cref="int"/>. Double values are
    /// accepted only when they have no fractional part and fit in an <see cref="int"/>.
    /// No other value kinds are converted.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<int> GetInt(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Integer when !value.IsUnrepresentable && TryLongToInt(value.AsLong(), out var fromInteger)
                => new(ConfigValueOutcome.Present, fromInteger, value.Position),
            ConfigValueKind.Double when TryDoubleToInt(value.AsDouble(), out var fromDouble)
                => new(ConfigValueOutcome.Present, fromDouble, value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Mapping or
            ConfigValueKind.Sequence or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="long"/> wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Integer values are accepted when they are representable as a <see cref="long"/>. Double
    /// values are accepted only when they have no fractional part and fit in a <see cref="long"/>.
    /// No other value kinds are converted.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<long> GetLong(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Integer when !value.IsUnrepresentable => new(ConfigValueOutcome.Present, value.AsLong(), value.Position),
            ConfigValueKind.Double when TryDoubleToLong(value.AsDouble(), out var fromDouble)
                => new(ConfigValueOutcome.Present, fromDouble, value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Mapping or
            ConfigValueKind.Sequence or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="double"/> wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Double values are accepted directly. Integer values are widened using the standard CLR
    /// conversion and may lose precision when their magnitude exceeds 2^53. No other value kinds
    /// are converted.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<double> GetDouble(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Double => new(ConfigValueOutcome.Present, value.AsDouble(), value.Position),
            ConfigValueKind.Integer when !value.IsUnrepresentable => new(ConfigValueOutcome.Present, value.AsLong(), value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Mapping or
            ConfigValueKind.Sequence or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a nested <see cref="ConfigProperties"/> mapping
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<ConfigProperties> GetMapping(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Mapping => new(ConfigValueOutcome.Present, value.AsMapping(), value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Double or
            ConfigValueKind.Integer or
            ConfigValueKind.Sequence or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="ConfigProperties"/> mappings
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element is a mapping. Any other element kind,
    /// including null or a nested sequence, makes the whole sequence a mismatch.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<IReadOnlyList<ConfigProperties>> GetMappingList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Sequence when TryBuildMappingList(value.AsSequence(), out var list)
                => new(ConfigValueOutcome.Present, list, value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Double or
            ConfigValueKind.Integer or
            ConfigValueKind.Mapping or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="string"/> values
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element has string kind. Any other element kind,
    /// including null, a nested sequence, or a mapping, makes the whole sequence a mismatch.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<IReadOnlyList<string>> GetStringList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new(ConfigValueOutcome.PresentNull, default, value.Position);
        }

        if (value.Kind == ConfigValueKind.Sequence && TryBuildStringList(value.AsSequence(), out var list))
        {
            return new(ConfigValueOutcome.Present, list, value.Position);
        }

        return new(ConfigValueOutcome.TypeMismatch, default, value.Position);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="bool"/> values
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element has boolean kind. Any other element kind,
    /// including null, a nested sequence, or a mapping, makes the whole sequence a mismatch.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<IReadOnlyList<bool>> GetBooleanList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new(ConfigValueOutcome.PresentNull, default, value.Position);
        }

        if (value.Kind == ConfigValueKind.Sequence && TryBuildBooleanList(value.AsSequence(), out var list))
        {
            return new(ConfigValueOutcome.Present, list, value.Position);
        }

        return new(ConfigValueOutcome.TypeMismatch, default, value.Position);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="int"/> values
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element is readable as <see cref="int"/>. Integer
    /// elements are accepted when they fit the <see cref="int"/> range; double elements are accepted
    /// when they have no fractional part and fit the range. Any other element kind, or a value
    /// outside the range, makes the whole sequence a mismatch.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<IReadOnlyList<int>> GetIntList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new(ConfigValueOutcome.PresentNull, default, value.Position);
        }

        if (value.Kind == ConfigValueKind.Sequence && TryBuildIntList(value.AsSequence(), out var list))
        {
            return new(ConfigValueOutcome.Present, list, value.Position);
        }

        return new(ConfigValueOutcome.TypeMismatch, default, value.Position);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="long"/> values
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element is readable as <see cref="long"/>. Integer
    /// elements are accepted directly; double elements are accepted when they have no fractional part
    /// and fit the <see cref="long"/> range. Any other element kind, or a value outside the range,
    /// makes the whole sequence a mismatch.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<IReadOnlyList<long>> GetLongList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new(ConfigValueOutcome.PresentNull, default, value.Position);
        }

        if (value.Kind == ConfigValueKind.Sequence && TryBuildLongList(value.AsSequence(), out var list))
        {
            return new(ConfigValueOutcome.Present, list, value.Position);
        }

        return new(ConfigValueOutcome.TypeMismatch, default, value.Position);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="double"/> values
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element is readable as <see cref="double"/>. Double
    /// elements are accepted directly; integer elements are widened. Any other element kind makes
    /// the whole sequence a mismatch.
    /// </remarks>
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ConfigValueResult<IReadOnlyList<double>> GetDoubleList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new(ConfigValueOutcome.PresentNull, default, value.Position);
        }

        if (value.Kind == ConfigValueKind.Sequence && TryBuildDoubleList(value.AsSequence(), out var list))
        {
            return new(ConfigValueOutcome.Present, list, value.Position);
        }

        return new(ConfigValueOutcome.TypeMismatch, default, value.Position);
    }

    internal static ConfigProperties Create(Dictionary<string, ConfigValue> values)
        => new(new Dictionary<string, ConfigValue>(values, StringComparer.Ordinal));

    internal bool TryGetValue(string key, out ConfigValue value)
    {
        Guard.ThrowIfNull(key);
        return this.values.TryGetValue(key, out value);
    }

    // long.MinValue = -2^63 is exactly representable; long.MaxValue = 2^63-1 rounds up to 2^63.
    private static bool TryDoubleToLong(double value, out long result)
    {
        if (double.IsNaN(value)
            || double.IsInfinity(value)
            || value != Math.Floor(value)
            || value is < -9223372036854775808.0 or >= 9223372036854775808.0)
        {
            result = 0;
            return false;
        }

        result = (long)value;
        return true;
    }

    private static bool TryDoubleToInt(double value, out int result)
    {
        if (double.IsNaN(value)
            || double.IsInfinity(value)
            || value != Math.Floor(value)
            || value is < int.MinValue or > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    private static bool TryLongToInt(long value, out int result)
    {
        if (value is < int.MinValue or > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    private static bool TryBuildMappingList(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<ConfigProperties>? result)
    {
        var list = new List<ConfigProperties>(sequence.Count);
        foreach (var item in sequence)
        {
            // A null element is a mismatch too: the element type is non-nullable.
            if (item.Kind != ConfigValueKind.Mapping)
            {
                result = null;
                return false;
            }

            list.Add(item.AsMapping());
        }

        result = list.AsReadOnly();
        return true;
    }

    private static bool TryBuildStringList(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<string>? result)
    {
        var list = new List<string>(sequence.Count);
        foreach (var item in sequence)
        {
            if (item.Kind != ConfigValueKind.String)
            {
                result = null;
                return false;
            }

            list.Add(item.AsString());
        }

        result = list.AsReadOnly();
        return true;
    }

    private static bool TryBuildBooleanList(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<bool>? result)
    {
        var list = new List<bool>(sequence.Count);
        foreach (var item in sequence)
        {
            if (item.Kind != ConfigValueKind.Boolean)
            {
                result = null;
                return false;
            }

            list.Add(item.AsBoolean());
        }

        result = list.AsReadOnly();
        return true;
    }

    private static bool TryBuildIntList(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<int>? result)
    {
        var list = new List<int>(sequence.Count);
        foreach (var item in sequence)
        {
            if (item.Kind == ConfigValueKind.Integer && !item.IsUnrepresentable && TryLongToInt(item.AsLong(), out var fromInteger))
            {
                list.Add(fromInteger);
            }
            else if (item.Kind == ConfigValueKind.Double && TryDoubleToInt(item.AsDouble(), out var fromDouble))
            {
                list.Add(fromDouble);
            }
            else
            {
                result = null;
                return false;
            }
        }

        result = list.AsReadOnly();
        return true;
    }

    private static bool TryBuildLongList(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<long>? result)
    {
        var list = new List<long>(sequence.Count);
        foreach (var item in sequence)
        {
            if (item.Kind == ConfigValueKind.Integer && !item.IsUnrepresentable)
            {
                list.Add(item.AsLong());
            }
            else if (item.Kind == ConfigValueKind.Double && TryDoubleToLong(item.AsDouble(), out var fromDouble))
            {
                list.Add(fromDouble);
            }
            else
            {
                result = null;
                return false;
            }
        }

        result = list.AsReadOnly();
        return true;
    }

    private static bool TryBuildDoubleList(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<double>? result)
    {
        var list = new List<double>(sequence.Count);
        foreach (var item in sequence)
        {
            if (item.Kind == ConfigValueKind.Double)
            {
                list.Add(item.AsDouble());
            }
            else if (item.Kind == ConfigValueKind.Integer && !item.IsUnrepresentable)
            {
                list.Add(item.AsLong());
            }
            else
            {
                result = null;
                return false;
            }
        }

        result = list.AsReadOnly();
        return true;
    }
}
