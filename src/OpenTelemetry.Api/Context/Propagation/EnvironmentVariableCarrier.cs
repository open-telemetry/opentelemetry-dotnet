// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
#if !NET
using System.Collections.ObjectModel;
#endif
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// A class containing helper methods for using environment variables as context propagation carriers.
/// </summary>
/// <remarks>
/// The carrier is format-agnostic and treats values as opaque strings. Key
/// normalization follows the OpenTelemetry environment carrier specification by
/// uppercasing ASCII letters, replacing non-ASCII letters, non-digits, and
/// non-underscore characters with underscores, prefixing an underscore when
/// a normalized key would otherwise start with a digit.
/// </remarks>
public static class EnvironmentVariableCarrier
{
    /// <summary>
    /// Captures a snapshot of the current process environment variables using
    /// normalized environment variable names.
    /// </summary>
    /// <returns>A read-only snapshot of the current environment variables.</returns>
    public static IReadOnlyDictionary<string, string?> Capture()
    {
        var environmentVariables = Environment.GetEnvironmentVariables();
        var carrier = new Dictionary<string, string?>(environmentVariables.Count, StringComparer.Ordinal);

        foreach (DictionaryEntry environmentVariable in environmentVariables)
        {
            carrier[NormalizeKey(Guard.ThrowIfNotOfType<string>(environmentVariable.Key))] = environmentVariable.Value?.ToString();
        }

#if NET
        return carrier.AsReadOnly();
#else
        return new ReadOnlyDictionary<string, string?>(carrier);
#endif
    }

    /// <summary>
    /// Captures a snapshot of the supplied environment variables using normalized
    /// environment variable names.
    /// </summary>
    /// <param name="environmentVariables">The environment variables to capture.</param>
    /// <returns>A read-only snapshot of the supplied environment variables.</returns>
    public static IReadOnlyDictionary<string, string?> Capture(IEnumerable<KeyValuePair<string, string?>> environmentVariables)
    {
        Guard.ThrowIfNull(environmentVariables);

        Dictionary<string, string?> carrier = environmentVariables is ICollection<KeyValuePair<string, string?>> collection
            ? new(collection.Count, StringComparer.Ordinal)
            : new(StringComparer.Ordinal);

        foreach (var environmentVariable in environmentVariables)
        {
            carrier[NormalizeKey(environmentVariable.Key)] = environmentVariable.Value;
        }

#if NET
        return carrier.AsReadOnly();
#else
        return new ReadOnlyDictionary<string, string?>(carrier);
#endif
    }

    /// <summary>
    /// Gets a value from an environment variable carrier using a normalized key.
    /// </summary>
    /// <typeparam name="T">The carrier type.</typeparam>
    /// <param name="carrier">The carrier to read.</param>
    /// <param name="key">The propagation key to look up.</param>
    /// <returns>
    /// A single-item sequence containing the value when the key exists;
    /// otherwise <see langword="null"/>.
    /// </returns>
    public static IEnumerable<string>? Get<T>(T carrier, string key)
        where T : IEnumerable<KeyValuePair<string, string?>>
    {
        Guard.ThrowIfNull(carrier);
        Guard.ThrowIfNull(key);

        var normalizedKey = NormalizeKey(key);

        if (carrier is IReadOnlyDictionary<string, string?> readOnlyDictionary &&
            readOnlyDictionary.TryGetValue(normalizedKey, out var readOnlyValue))
        {
            return ToEnumerable(readOnlyValue);
        }

        if (carrier is IDictionary<string, string?> dictionary &&
            dictionary.TryGetValue(normalizedKey, out var dictionaryValue))
        {
            return ToEnumerable(dictionaryValue);
        }

        foreach (var entry in carrier)
        {
            if (IsNormalizedMatch(entry.Key, normalizedKey.AsSpan()))
            {
                return ToEnumerable(entry.Value);
            }
        }

        return null;

        static IEnumerable<string>? ToEnumerable(string? value)
        {
            return value is null ? null : [value];
        }
    }

    /// <summary>
    /// Sets a value on an environment variable carrier using a normalized key.
    /// </summary>
    /// <typeparam name="T">The carrier type.</typeparam>
    /// <param name="carrier">The carrier to write.</param>
    /// <param name="key">The propagation key to normalize and store.</param>
    /// <param name="value">The value to store.</param>
    public static void Set<T>(T carrier, string key, string value)
        where T : IDictionary<string, string?>
    {
        Guard.ThrowIfNull(carrier);
        Guard.ThrowIfNull(key);
        Guard.ThrowIfNull(value);

        carrier[NormalizeKey(key)] = value;
    }

    /// <summary>
    /// Normalizes a propagation key to an environment variable name.
    /// </summary>
    /// <param name="key">The key to normalize.</param>
    /// <returns>The normalized environment variable name.</returns>
    public static string NormalizeKey(string key)
    {
        Guard.ThrowIfNull(key);

        if (IsAlreadyNormalized(key))
        {
            return key;
        }

        var prefixLength = IsAsciiDigit(key[0]) ? 1 : 0;
        return CreateNormalizedKey(key, prefixLength);
    }

    private static bool IsAlreadyNormalized(string key)
    {
        if (key.Length == 0 || IsAsciiDigit(key[0]))
        {
            return key.Length == 0;
        }

        foreach (var ch in key)
        {
            if (!IsNormalized(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNormalizedMatch(string candidateKey, ReadOnlySpan<char> normalizedKey)
    {
        var candidateLength = candidateKey.Length;
        var normalizedIndex = 0;

        if (candidateLength > 0 && IsAsciiDigit(candidateKey[0]))
        {
            if (normalizedKey.IsEmpty || normalizedKey[0] != '_')
            {
                return false;
            }

            normalizedIndex = 1;
        }

        if (candidateLength + normalizedIndex != normalizedKey.Length)
        {
            return false;
        }

        for (var candidateIndex = 0; candidateIndex < candidateLength; candidateIndex++, normalizedIndex++)
        {
            if (NormalizeCharacter(candidateKey[candidateIndex]) != normalizedKey[normalizedIndex])
            {
                return false;
            }
        }

        return true;
    }

    private static string CreateNormalizedKey(string key, int prefixLength)
    {
        int length = key.Length + prefixLength;

#if NETSTANDARD2_1_OR_GREATER || NET
        return string.Create(length, (key, prefixLength), static (buffer, state) =>
        {
            if (state.prefixLength == 1)
            {
                buffer[0] = '_';
            }

            for (var i = 0; i < state.key.Length; i++)
            {
                buffer[i + state.prefixLength] = NormalizeCharacter(state.key[i]);
            }
        });
#else
        var normalizedKey = new char[length];

        if (prefixLength == 1)
        {
            normalizedKey[0] = '_';
        }

        for (var i = 0; i < key.Length; i++)
        {
            normalizedKey[i + prefixLength] = NormalizeCharacter(key[i]);
        }

        return new string(normalizedKey);
#endif
    }

    private static char NormalizeCharacter(char value)
    {
#if NET
        if (char.IsAsciiLetterLower(value))
#else
        if (value >= 'a' && value <= 'z')
#endif
        {
            return (char)(value - 32);
        }

        if (IsNormalized(value))
        {
            return value;
        }

        // Replace anything outside the normalized range with an underscore
        return '_';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNormalized(char value) =>
        IsAsciiLetterUpper(value) || IsAsciiDigit(value) || value == '_';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterUpper(char value) =>
#if NET
        char.IsAsciiLetterUpper(value);
#else
       value >= 'A' && value <= 'Z';
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(char value) =>
#if NET
        char.IsAsciiDigit(value);
#else
        value >= '0' && value <= '9';
#endif
}
