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
        new ConfigProperties(new Dictionary<string, ConfigValue>(0, StringComparer.Ordinal));

    /// <summary>
    /// Gets all keys present in this mapping.
    /// </summary>
    public IReadOnlyCollection<string> Keys => this.values.Keys;

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="string"/>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <returns>A result with outcome <see cref="ConfigValueOutcome.Absent"/>, <see cref="ConfigValueOutcome.PresentNull"/>, <see cref="ConfigValueOutcome.Present"/>, or <see cref="ConfigValueOutcome.TypeMismatch"/>.</returns>
    public ConfigValueResult<string> GetString(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<string>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<string>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind == ConfigValueKind.String)
        {
            return new ConfigValueResult<string>(ConfigValueOutcome.Present, value.AsString());
        }

        return new ConfigValueResult<string>(ConfigValueOutcome.TypeMismatch, default);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="bool"/>.
    /// </summary>
    /// <inheritdoc cref="GetString" path="/param | /returns"/>
    public ConfigValueResult<bool> GetBoolean(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<bool>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<bool>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind == ConfigValueKind.Boolean)
        {
            return new ConfigValueResult<bool>(ConfigValueOutcome.Present, value.AsBoolean());
        }

        return new ConfigValueResult<bool>(ConfigValueOutcome.TypeMismatch, default);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as an <see cref="int"/>.
    /// </summary>
    /// <inheritdoc cref="GetString" path="/param | /returns"/>
    public ConfigValueResult<int> GetInt(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<int>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<int>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind == ConfigValueKind.Integer && !value.IsUnrepresentable)
        {
            var longValue = value.AsLong();
            if (longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return new ConfigValueResult<int>(ConfigValueOutcome.Present, (int)longValue);
            }
        }

        if (value.Kind == ConfigValueKind.Float && TryDoubleToInt(value.AsDouble(), out var intResult))
        {
            return new ConfigValueResult<int>(ConfigValueOutcome.Present, intResult);
        }

        return new ConfigValueResult<int>(ConfigValueOutcome.TypeMismatch, default);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="long"/>.
    /// </summary>
    /// <inheritdoc cref="GetString" path="/param | /returns"/>
    public ConfigValueResult<long> GetLong(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<long>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<long>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind == ConfigValueKind.Integer && !value.IsUnrepresentable)
        {
            return new ConfigValueResult<long>(ConfigValueOutcome.Present, value.AsLong());
        }

        if (value.Kind == ConfigValueKind.Float && TryDoubleToLong(value.AsDouble(), out var longResult))
        {
            return new ConfigValueResult<long>(ConfigValueOutcome.Present, longResult);
        }

        return new ConfigValueResult<long>(ConfigValueOutcome.TypeMismatch, default);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a <see cref="double"/>.
    /// </summary>
    /// <inheritdoc cref="GetString" path="/param | /returns"/>
    public ConfigValueResult<double> GetDouble(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<double>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<double>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind == ConfigValueKind.Float)
        {
            return new ConfigValueResult<double>(ConfigValueOutcome.Present, value.AsDouble());
        }

        if (value.Kind == ConfigValueKind.Integer && !value.IsUnrepresentable)
        {
            return new ConfigValueResult<double>(ConfigValueOutcome.Present, (double)value.AsLong());
        }

        return new ConfigValueResult<double>(ConfigValueOutcome.TypeMismatch, default);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a nested <see cref="ConfigProperties"/> mapping.
    /// </summary>
    /// <inheritdoc cref="GetString" path="/param | /returns"/>
    public ConfigValueResult<ConfigProperties> GetProperties(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<ConfigProperties>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<ConfigProperties>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind == ConfigValueKind.Mapping)
        {
            return new ConfigValueResult<ConfigProperties>(ConfigValueOutcome.Present, value.AsMapping());
        }

        return new ConfigValueResult<ConfigProperties>(ConfigValueOutcome.TypeMismatch, default);
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of <see cref="ConfigProperties"/> mappings.
    /// </summary>
    /// <inheritdoc cref="GetString" path="/param | /returns"/>
    public ConfigValueResult<IReadOnlyList<ConfigProperties>> GetPropertiesList(string key)
    {
        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<IReadOnlyList<ConfigProperties>>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<IReadOnlyList<ConfigProperties>>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind != ConfigValueKind.Sequence)
        {
            return new ConfigValueResult<IReadOnlyList<ConfigProperties>>(ConfigValueOutcome.TypeMismatch, default);
        }

        var sequence = value.AsSequence();
        var list = new List<ConfigProperties>(sequence.Count);
        foreach (var item in sequence)
        {
            // Null elements are also a mismatch: IReadOnlyList<ConfigProperties> has no slot for null.
            // GetScalarList<T> applies the same rule.
            if (item.Kind != ConfigValueKind.Mapping)
            {
                return new ConfigValueResult<IReadOnlyList<ConfigProperties>>(ConfigValueOutcome.TypeMismatch, default);
            }

            list.Add(item.AsMapping());
        }

        return new ConfigValueResult<IReadOnlyList<ConfigProperties>>(ConfigValueOutcome.Present, list.AsReadOnly());
    }

    /// <summary>
    /// Returns the value of <paramref name="key"/> as a list of scalar values of type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// A sequence is readable only when every element is a scalar readable as <typeparamref name="T"/>.
    /// A null element, an element of another kind, and a nested sequence or mapping each make the whole
    /// sequence a mismatch, as they do for <see cref="GetPropertiesList"/>.
    /// <para>
    /// Element coercion mirrors the scalar getters: <see cref="long"/>, <see cref="double"/>, and
    /// <see cref="int"/> elements accept numeric widening (e.g. a YAML float element is accepted for
    /// <c>GetScalarList&lt;long&gt;</c> when it has no fractional part and fits the target range);
    /// <see cref="string"/> and <see cref="bool"/> elements require an exact kind match.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The scalar element type. Supported types are <see cref="string"/>, <see cref="bool"/>, <see cref="long"/>, <see cref="double"/>, and <see cref="int"/>.</typeparam>
    /// <param name="key">The key to read.</param>
    /// <returns>A result with outcome <see cref="ConfigValueOutcome.Absent"/>, <see cref="ConfigValueOutcome.PresentNull"/>, <see cref="ConfigValueOutcome.Present"/>, or <see cref="ConfigValueOutcome.TypeMismatch"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when <typeparamref name="T"/> is not one of the five supported scalar types. This is a programming error.</exception>
    public ConfigValueResult<IReadOnlyList<T>> GetScalarList<T>(string key)
    {
        if (typeof(T) != typeof(string) && typeof(T) != typeof(bool) && typeof(T) != typeof(long)
            && typeof(T) != typeof(double) && typeof(T) != typeof(int))
        {
            throw new NotSupportedException(
                $"'{typeof(T).Name}' is not a supported scalar element type. Supported types are string, bool, long, double, and int.");
        }

        if (!this.TryGetValue(key, out var value))
        {
            return new ConfigValueResult<IReadOnlyList<T>>(ConfigValueOutcome.Absent, default);
        }

        if (value.Kind == ConfigValueKind.Null)
        {
            return new ConfigValueResult<IReadOnlyList<T>>(ConfigValueOutcome.PresentNull, default);
        }

        if (value.Kind != ConfigValueKind.Sequence)
        {
            return new ConfigValueResult<IReadOnlyList<T>>(ConfigValueOutcome.TypeMismatch, default);
        }

        var sequence = value.AsSequence();
        var list = new List<T>(sequence.Count);
        for (var i = 0; i < sequence.Count; i++)
        {
            var item = sequence[i];

            // A null element mismatches the whole sequence, as it does for GetPropertiesList. An
            // unconstrained T? is a nullable annotation only - for a value type it is T itself - so a
            // null element could not be represented here without either corrupting it into default(T)
            // or splitting the accessor per element type. No scalar sequence in the configuration
            // schema permits a null element, so nothing readable is lost.
            if (item.Kind == ConfigValueKind.Null)
            {
                return new ConfigValueResult<IReadOnlyList<T>>(ConfigValueOutcome.TypeMismatch, default);
            }

            if (!TryExtractScalar<T>(item, out var scalar))
            {
                return new ConfigValueResult<IReadOnlyList<T>>(ConfigValueOutcome.TypeMismatch, default);
            }

            list.Add(scalar!);
        }

        return new ConfigValueResult<IReadOnlyList<T>>(ConfigValueOutcome.Present, list.AsReadOnly());
    }

    internal static ConfigProperties Create(Dictionary<string, ConfigValue> values)
        => new ConfigProperties(new Dictionary<string, ConfigValue>(values, StringComparer.Ordinal));

    private static bool TryDoubleToLong(double value, out long result)
    {
        // long.MinValue = -2^63 is exactly representable; long.MaxValue = 2^63-1 rounds up to 2^63.
        if (double.IsNaN(value) || double.IsInfinity(value) || value != Math.Floor(value)
            || value < -9223372036854775808.0 || value >= 9223372036854775808.0)
        {
            result = 0;
            return false;
        }

        result = (long)value;
        return true;
    }

    private static bool TryDoubleToInt(double value, out int result)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value != Math.Floor(value)
            || value < int.MinValue || value > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    private static bool TryExtractScalar<T>(ConfigValue value, out T? result)
    {
        if (typeof(T) == typeof(string))
        {
            if (value.Kind == ConfigValueKind.String)
            {
                result = (T)(object)value.AsString();
                return true;
            }
        }
        else if (typeof(T) == typeof(bool))
        {
            if (value.Kind == ConfigValueKind.Boolean)
            {
                result = (T)(object)value.AsBoolean();
                return true;
            }
        }
        else if (typeof(T) == typeof(long))
        {
            if (value.Kind == ConfigValueKind.Integer && !value.IsUnrepresentable)
            {
                result = (T)(object)value.AsLong();
                return true;
            }

            if (value.Kind == ConfigValueKind.Float && TryDoubleToLong(value.AsDouble(), out var longValue))
            {
                result = (T)(object)longValue;
                return true;
            }
        }
        else if (typeof(T) == typeof(double))
        {
            if (value.Kind == ConfigValueKind.Float)
            {
                result = (T)(object)value.AsDouble();
                return true;
            }

            if (value.Kind == ConfigValueKind.Integer && !value.IsUnrepresentable)
            {
                result = (T)(object)(double)value.AsLong();
                return true;
            }
        }
        else if (typeof(T) == typeof(int))
        {
            if (value.Kind == ConfigValueKind.Integer && !value.IsUnrepresentable)
            {
                var longValue = value.AsLong();
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    result = (T)(object)(int)longValue;
                    return true;
                }
            }

            if (value.Kind == ConfigValueKind.Float && TryDoubleToInt(value.AsDouble(), out var intValue))
            {
                result = (T)(object)intValue;
                return true;
            }
        }

        result = default;
        return false;
    }

    private bool TryGetValue(string key, out ConfigValue value)
        => this.values.TryGetValue(key, out value);
}
