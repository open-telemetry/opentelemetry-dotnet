// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// An immutable, typed view over a flat mapping of configuration keys to <see cref="ConfigValue"/>s.
/// </summary>
internal sealed class ConfigProperties
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
    public IReadOnlyCollection<string> Keys => this.values.Keys;

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="string"/> wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <returns>
    /// A result with outcome <see cref="ConfigValueOutcome.Absent"/>, <see cref="ConfigValueOutcome.PresentNull"/>,
    /// <see cref="ConfigValueOutcome.Present"/>, or <see cref="ConfigValueOutcome.TypeMismatch"/>.
    /// </returns>
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
    public ConfigValueResult<bool> GetBoolean(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Boolean => new(ConfigValueOutcome.Present, value.AsBoolean(), value.Position),
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
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
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
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
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
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
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
    public ConfigValueResult<ConfigProperties> GetProperties(string key)
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
    /// <param name="key"><inheritdoc cref="GetString" path="/param"/></param>
    /// <returns><inheritdoc cref="GetString" path="/returns"/></returns>
    public ConfigValueResult<IReadOnlyList<ConfigProperties>> GetPropertiesList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Sequence when TryBuildPropertiesList(value.AsSequence(), out var list)
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
    /// Returns the value of <paramref name="key"/> as a list of scalar values of type <typeparamref name="T"/>
    /// wrapped as a <see cref="ConfigValueResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element is a scalar readable as <typeparamref name="T"/>.
    /// Any element of another kind - including null, a nested sequence, or a mapping - makes the whole
    /// sequence a mismatch.
    /// <para>
    /// Element coercion mirrors the scalar getters: <see cref="long"/>, <see cref="double"/>, and
    /// <see cref="int"/> elements accept cross-kind numeric conversion where the value survives it
    /// (e.g. a YAML float element is accepted for <c>GetScalarList&lt;long&gt;</c> when it has no
    /// fractional part and fits the target range); <see cref="string"/> and <see cref="bool"/>
    /// elements require an exact kind match.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">
    /// The scalar element type. Supported types are <see cref="string"/>, <see cref="bool"/>,
    /// <see cref="long"/>, <see cref="double"/>, and <see cref="int"/>.
    /// </typeparam>
    /// <param name="key">The key to read.</param>
    /// <returns>
    /// A result with outcome <see cref="ConfigValueOutcome.Absent"/>, <see cref="ConfigValueOutcome.PresentNull"/>,
    /// <see cref="ConfigValueOutcome.Present"/>, or <see cref="ConfigValueOutcome.TypeMismatch"/>.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is not one of the five supported scalar types. This is a
    /// programming error.
    /// </exception>
    public ConfigValueResult<IReadOnlyList<T>> GetScalarList<T>(string key)
    {
        if (typeof(T) != typeof(string)
            && typeof(T) != typeof(bool)
            && typeof(T) != typeof(long)
            && typeof(T) != typeof(double)
            && typeof(T) != typeof(int))
        {
            throw new NotSupportedException(
                $"'{typeof(T).Name}' is not a supported scalar element type. Supported types are string, bool, long, double, and int.");
        }

        if (!this.TryGetValue(key, out var value))
        {
            return new(ConfigValueOutcome.Absent, default, ConfigValuePosition.Unknown);
        }

        return value.Kind switch
        {
            ConfigValueKind.Null => new(ConfigValueOutcome.PresentNull, default, value.Position),
            ConfigValueKind.Sequence when TryBuildScalarList<T>(value.AsSequence(), out var list)
                => new(ConfigValueOutcome.Present, list, value.Position),
            ConfigValueKind.Boolean or
            ConfigValueKind.Double or
            ConfigValueKind.Integer or
            ConfigValueKind.Mapping or
            ConfigValueKind.String or
            _ => new(ConfigValueOutcome.TypeMismatch, default, value.Position),
        };
    }

    internal static ConfigProperties Create(Dictionary<string, ConfigValue> values)
        => new(new Dictionary<string, ConfigValue>(values, StringComparer.Ordinal));

    private static bool TryDoubleToLong(double value, out long result)
    {
        // long.MinValue = -2^63 is exactly representable; long.MaxValue = 2^63-1 rounds up to 2^63.
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

    private static bool TryBuildPropertiesList(
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

    private static bool TryBuildScalarList<T>(
        IReadOnlyList<ConfigValue> sequence,
        out IReadOnlyList<T>? result)
    {
        var list = new List<T>(sequence.Count);
        foreach (var item in sequence)
        {
            // A null element is a mismatch: representing one would need IReadOnlyList<T?>, which for a
            // value-type T means Nullable<T> and a per-type accessor split.
            if (item.Kind == ConfigValueKind.Null || !TryExtractScalar<T>(item, out var scalar))
            {
                result = null;
                return false;
            }

            list.Add(scalar!);
        }

        result = list.AsReadOnly();
        return true;
    }

    // A null scalar means no arm matched. Unambiguous because no arm can yield null: ConfigValue.String
    // rejects a null payload.
    private static bool TryExtractScalar<T>(ConfigValue value, out T? result)
    {
        object? scalar = value.Kind switch
        {
            ConfigValueKind.String when typeof(T) == typeof(string) => value.AsString(),
            ConfigValueKind.Boolean when typeof(T) == typeof(bool) => value.AsBoolean(),
            ConfigValueKind.Integer when typeof(T) == typeof(long) && !value.IsUnrepresentable => value.AsLong(),
            ConfigValueKind.Integer when typeof(T) == typeof(double) && !value.IsUnrepresentable => (double)value.AsLong(),
            ConfigValueKind.Integer when typeof(T) == typeof(int)
                && !value.IsUnrepresentable
                && TryLongToInt(value.AsLong(), out var intFromLong) => intFromLong,
            ConfigValueKind.Double when typeof(T) == typeof(double) => value.AsDouble(),
            ConfigValueKind.Double when typeof(T) == typeof(long)
                && TryDoubleToLong(value.AsDouble(), out var longFromDouble) => longFromDouble,
            ConfigValueKind.Double when typeof(T) == typeof(int)
                && TryDoubleToInt(value.AsDouble(), out var intFromDouble) => intFromDouble,
            ConfigValueKind.Mapping or
            ConfigValueKind.Null or
            ConfigValueKind.Sequence or
            _ => null,
        };

        if (scalar is null)
        {
            result = default;
            return false;
        }

        result = (T)scalar;
        return true;
    }

    private bool TryGetValue(string key, out ConfigValue value)
        => this.values.TryGetValue(key, out value);
}
