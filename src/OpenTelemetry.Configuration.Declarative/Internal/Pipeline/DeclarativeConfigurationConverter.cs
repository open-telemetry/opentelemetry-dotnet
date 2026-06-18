// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Converts a typed <see cref="DeclarativeConfiguration"/> model into the flat, env-var-style
/// key/value pairs consumed by the OpenTelemetry SDK's IConfiguration readers.
/// </summary>
/// <remarks>
/// This is a lossy, one-way conversion: only the fields expressible in the env-var format are
/// emitted. Fields absent or present-null in the model produce no output, leaving SDK defaults
/// and other IConfiguration sources in effect.
/// </remarks>
internal static partial class DeclarativeConfigurationConverter
{
    internal const string DisabledKey = OtelEnvironmentVariables.SdkDisabled;
    internal const string ResourceAttributesKey = OtelEnvironmentVariables.ResourceAttributes;

    // Per OTel attribute naming spec: starts with a letter or underscore,
    // followed by letters, digits, underscores, hyphens, or dots.
    private const string AttributeNamePatternString = @"^[a-zA-Z_][-a-zA-Z0-9_.]*$";

    // OTel declarative config spec type field values. Scalar types project to a flat string value;
    // array types cannot be represented in OTEL_RESOURCE_ATTRIBUTES and must be skipped.
    private static readonly HashSet<string> KnownScalarTypes = new(StringComparer.Ordinal)
    {
        "string", "bool", "int", "double",
    };

    private static readonly HashSet<string> KnownArrayTypes = new(StringComparer.Ordinal)
    {
        "string_array", "bool_array", "int_array", "double_array",
    };

#if !NET
    private static readonly Regex AttributeNamePatternInstance = new(
        AttributeNamePatternString,
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromSeconds(1));
#endif

    /// <summary>
    /// Converts <paramref name="config"/> into <paramref name="data"/> as flat OTel configuration keys.
    /// </summary>
    /// <param name="config">The typed configuration model to convert.</param>
    /// <param name="data">Dictionary to populate with flat key/value pairs.</param>
    internal static void Convert(DeclarativeConfiguration config, IDictionary<string, string?> data)
    {
        EmitDisabled(config.Disabled, data);
        EmitResource(config.Resource, data);
    }

    // disabled -> OTEL_SDK_DISABLED. Present emits canonical true/false; null/absent emit nothing.
    private static void EmitDisabled(ConfigProperty<bool> disabled, IDictionary<string, string?> data)
    {
        if (disabled.TryGetValue(out var value))
        {
            data[DisabledKey] = value ? "true" : "false";
        }
    }

    // resource.attributes / resource.attributes_list -> OTEL_RESOURCE_ATTRIBUTES.
    private static void EmitResource(ConfigProperty<ResourceConfiguration> resource, IDictionary<string, string?> data)
    {
        if (!resource.TryGetValue(out var resourceConfig))
        {
            return;
        }

        // attributes_list: a pre-encoded OTEL_RESOURCE_ATTRIBUTES-format string, passed through as-is.
        // Empty/whitespace is treated as no list.
        string? list = null;
        if (resourceConfig.AttributesList.TryGetValue(out var rawList))
        {
            var trimmed = rawList.Trim();
            if (trimmed.Length > 0)
            {
                list = trimmed;
            }
        }

        // attributes: structured entries; attributes_list is pre-encoded passthrough (lower priority).
        var pairs = new List<string>();
        var attributeKeys = new HashSet<string>(StringComparer.Ordinal);
        if (resourceConfig.Attributes.TryGetValue(out var entries))
        {
            foreach (var entry in entries)
            {
                if (entry.Name is null)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        "A resource.attributes entry is missing the required 'name' field and will be skipped.");
                    continue;
                }

                // Unrecognized type: skip (authoring error).
                if (entry.RawType is not null &&
                    !KnownScalarTypes.Contains(entry.RawType) &&
                    !KnownArrayTypes.Contains(entry.RawType))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has an unrecognized type '{entry.RawType}' " +
                        "and will be skipped. Valid types: string, bool, int, double, string_array, bool_array, int_array, double_array.");
                    continue;
                }

                // Array/sequence: not representable in OTEL_RESOURCE_ATTRIBUTES.
                if ((entry.RawType is not null && KnownArrayTypes.Contains(entry.RawType)) ||
                    entry.ValueNodeKind == AttributeValueNodeKind.Sequence)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has an array value which cannot be " +
                        "represented in OTEL_RESOURCE_ATTRIBUTES format and will be skipped.");
                    continue;
                }

                // Mapping value: not representable in flat format.
                if (entry.ValueNodeKind == AttributeValueNodeKind.Mapping)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has a mapping value which cannot be " +
                        "represented in OTEL_RESOURCE_ATTRIBUTES format and will be skipped.");
                    continue;
                }

                // NullScalar: key present, no usable value.
                if (entry.ValueNodeKind == AttributeValueNodeKind.NullScalar)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has a null 'value' field and will be skipped.");
                    continue;
                }

                // Value key is absent from the entry - the attribute is incomplete.
                if (!entry.TryGetScalarValue(out var scalarValue))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' is missing the required 'value' field and will be skipped.");
                    continue;
                }

                // Hard reject: ',' or '=' in the name would corrupt the OTEL_RESOURCE_ATTRIBUTES flat
                // key=value,key=value format consumed by OtelEnvResourceDetector.
#if NETFRAMEWORK || NETSTANDARD2_0
                if (entry.Name.IndexOf(',') >= 0 || entry.Name.IndexOf('=') >= 0)
#else
                if (entry.Name.Contains(',', StringComparison.Ordinal) || entry.Name.Contains('=', StringComparison.Ordinal))
#endif
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry has a name '{entry.Name}' that contains ',' or '=' " +
                        "which would corrupt the OTEL_RESOURCE_ATTRIBUTES flat format and will be skipped.");
                    continue;
                }

                // Soft warn: other non-convention names are emitted verbatim. The naming spec
                // ([a-zA-Z_][-a-zA-Z0-9_.]*) is advisory for the SDK; only ',' and '=' are
                // structurally prohibited by the flat-format projection.
                if (!GetAttributeNamePattern().IsMatch(entry.Name))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.ResourceAttributeNameNotCompliant(entry.Name);
                }

                // Type mismatch: warn only (type is informational per spec).
                if (entry.RawType is not null && !IsValueConsistentWithType(entry.RawType, scalarValue))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.ResourceAttributeValueTypeMismatch(
                        entry.Name, entry.RawType, scalarValue);
                }

                // Duplicate name: first wins.
                if (!attributeKeys.Add(entry.Name))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.DuplicateResourceAttributeName(entry.Name);
                    continue;
                }

                pairs.Add($"{entry.Name}={EncodeAttributeValue(scalarValue)}");
            }
        }

        // Merge attributes_list (filtered) with attributes; attributes win on key collision.
        string? result;
        if (list is not null && pairs.Count > 0)
        {
            var filtered = FilterAttributesList(list, attributeKeys);
            result = filtered.Length > 0
                ? $"{filtered},{string.Join(",", pairs)}"
                : string.Join(",", pairs);
        }
        else if (pairs.Count > 0)
        {
            result = string.Join(",", pairs);
        }
        else
        {
            result = list; // null if both absent
        }

        if (result is not null)
        {
            data[ResourceAttributesKey] = result;
        }
    }

    // Percent-encode attribute values for OTEL_RESOURCE_ATTRIBUTES per the OTel resource spec:
    // https://opentelemetry.io/docs/specs/otel/resource/sdk/#specifying-resource-information-via-an-environment-variable
    // Encoding order: '%' first to prevent double-encoding, then structural chars ',' and '=',
    // then '+' because the .NET SDK reads the env var via WebUtility.UrlDecode which maps '+' to space.
    private static string EncodeAttributeValue(string value)
    {
        var sb = new StringBuilder(value);
        sb.Replace("%", "%25");
        sb.Replace(",", "%2C");
        sb.Replace("=", "%3D");
        sb.Replace("+", "%2B");
        return sb.ToString();
    }

    // Drop attributes_list keys shadowed by structured attributes. Naive comma split (matches OtelEnvResourceDetector).
    private static string FilterAttributesList(string list, HashSet<string> attributeKeys)
    {
        var filtered = new List<string>();
        foreach (var part in list.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

#if NETFRAMEWORK || NETSTANDARD2_0
            var equalsIndex = trimmed.IndexOf('=');
#else
            var equalsIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
#endif
            var index = equalsIndex >= 0 ? trimmed.Substring(0, equalsIndex).Trim() : trimmed;
            if (!attributeKeys.Contains(index))
            {
                filtered.Add(trimmed);
            }
        }

        return string.Join(",", filtered);
    }

    // Type field is informational: mismatch logs a warning but does not skip the entry.
    private static bool IsValueConsistentWithType(string type, string value) =>
        type switch
        {
            "bool" => bool.TryParse(value, out _),
            "int" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "double" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            _ => true, // "string": any value is valid.
        };

#if NET
    [GeneratedRegex(AttributeNamePatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex GetAttributeNamePattern();
#else
    private static Regex GetAttributeNamePattern() => AttributeNamePatternInstance;
#endif
}
