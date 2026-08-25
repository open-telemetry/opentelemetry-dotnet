// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    private static void EmitDisabled(ModelProperty<bool> disabled, IDictionary<string, string?> data)
    {
        if (disabled.TryGetValue(out var value))
        {
            data[DisabledKey] = value ? "true" : "false";
        }
    }

    // resource.attributes / resource.attributes_list -> OTEL_RESOURCE_ATTRIBUTES.
    private static void EmitResource(ModelProperty<ResourceConfiguration> resource, IDictionary<string, string?> data)
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

        // Two distinct sets, deliberately. Names are normalized as OtelEnvResourceDetector does
        // when parsing OTEL_RESOURCE_ATTRIBUTES. declaredNames records every name the author
        // declared in resource.attributes, including entries this projection cannot carry; it
        // drives the attributes_list shadowing below, because resource.attributes outranks
        // resource.attributes_list whether or not the higher-priority value survives projection.
        // projectedNames records only names actually emitted, and drives the first-wins duplicate
        // policy. Conflating the two makes a skipped entry suppress a later projectable one with
        // the same name.
        var declaredNames = new HashSet<string>(StringComparer.Ordinal);
        var projectedNames = new HashSet<string>(StringComparer.Ordinal);
        if (resourceConfig.Attributes.TryGetValue(out var entries))
        {
            foreach (var entry in entries)
            {
                // NullScalar: schema v1.1 says a present-but-null value means the entry is ignored,
                // so it declares nothing and must not shadow an attributes_list entry of the same
                // name. Checked before the name reservation below for exactly that reason.
                if (entry.ValueNodeKind == AttributeValueNodeKind.NullScalar)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has a null 'value' field and will be skipped.");
                    continue;
                }

                // Hard reject: ',' or '=' in the name would corrupt the OTEL_RESOURCE_ATTRIBUTES flat
                // key=value,key=value format consumed by OtelEnvResourceDetector. Such a name can
                // never equal a comma-split attributes_list key either, so it reserves nothing.
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

                // Everything from here on is a validly declared attribute, so reserve the name
                // before the representability checks. Without this, a declared-but-unprojectable
                // attribute lets the lower-priority attributes_list value through in its place.
                // The SDK trims keys in the flat format, so use the normalized name whenever
                // resolving attributes_list precedence or projected-name duplicates.
                var normalizedName = entry.Name.Trim();
                declaredNames.Add(normalizedName);

                // Array/sequence: not representable in OTEL_RESOURCE_ATTRIBUTES.
                if (entry.ValueNodeKind == AttributeValueNodeKind.Sequence)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has an array value which cannot be " +
                        "represented in OTEL_RESOURCE_ATTRIBUTES format and will be skipped.");
                    continue;
                }

                // The IConfiguration bridge feeds OTEL_RESOURCE_ATTRIBUTES, whose .NET reader
                // creates string-valued attributes. Emitting a valid bool/int/double here would
                // silently change its type, so unsupported non-string values must be skipped.
                if (entry.Type != ResourceAttributeType.String)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                        $"A resource.attributes entry for '{entry.Name}' has type " +
                        $"'{entry.Type.GetSchemaName()}', which cannot be represented by the " +
                        "OTEL_RESOURCE_ATTRIBUTES bridge without losing its type, and will be skipped.");
                    continue;
                }

                if (!entry.TryGetScalarValue(out var scalarValue))
                {
                    throw new InvalidOperationException(
                        $"Resource attribute '{entry.Name}' has an inconsistent internal value representation.");
                }

                // Soft warn: other non-convention names are emitted verbatim. The naming spec
                // ([a-zA-Z_][-a-zA-Z0-9_.]*) is advisory for the SDK; only ',' and '=' are
                // structurally prohibited by the flat-format projection.
                if (!GetAttributeNamePattern().IsMatch(entry.Name))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.ResourceAttributeNameNotCompliant(entry.Name);
                }

                // Duplicate name: first wins.
                if (!projectedNames.Add(normalizedName))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.DuplicateResourceAttributeName(entry.Name);
                    continue;
                }

                pairs.Add($"{entry.Name}={EncodeAttributeValue(scalarValue)}");
            }
        }

        // Drop attributes_list entries shadowed by a declared name. This runs whenever any name was
        // declared, not only when one was projected: the schema states that resource.attributes
        // entries "have higher priority than entries from .resource.attributes_list", so a declared
        // name must suppress the list entry rather than silently fall back to it.
        if (list is not null && declaredNames.Count > 0)
        {
            var filtered = FilterAttributesList(list, declaredNames);
            list = filtered.Length > 0 ? filtered : null;
        }

        // Merge attributes_list (filtered) with attributes; attributes win on key collision.
        // When nothing survives on either side the key is left unset, so lower-priority
        // IConfiguration sources stay in effect rather than being overridden with an empty value.
        string? result;
        if (list is not null && pairs.Count > 0)
        {
            result = $"{list},{JoinWithComma(pairs)}";
        }
        else if (pairs.Count > 0)
        {
            result = JoinWithComma(pairs);
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
    // Percent-encode structural characters, '%' to prevent unintended decoding, and '+' because
    // WebUtility.UrlDecode maps it to space. OtelEnvResourceDetector trims values before decoding,
    // so only leading/trailing whitespace needs encoding to survive that trim; interior whitespace
    // passes through as a literal and round-trips correctly through UrlDecode unchanged.
    private static string EncodeAttributeValue(string value)
    {
        // Locate the interior (non-trimmed) span so that only boundary whitespace is encoded.
        int innerStart = 0;
        while (innerStart < value.Length && char.IsWhiteSpace(value[innerStart]))
        {
            innerStart++;
        }

        int innerEnd = value.Length - 1;
        while (innerEnd >= innerStart && char.IsWhiteSpace(value[innerEnd]))
        {
            innerEnd--;
        }

        var encoded = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '%':
                    encoded.Append("%25");
                    break;
                case ',':
                    encoded.Append("%2C");
                    break;
                case '=':
                    encoded.Append("%3D");
                    break;
                case '+':
                    encoded.Append("%2B");
                    break;
                default:
                    if (char.IsWhiteSpace(c) && (i < innerStart || i > innerEnd))
                    {
                        encoded.Append(Uri.EscapeDataString(c.ToString()));
                    }
                    else
                    {
                        encoded.Append(c);
                    }

                    break;
            }
        }

        return encoded.ToString();
    }

    // Drop attributes_list keys shadowed by structured attributes. Naive comma split, and the key
    // is trimmed before comparison, both matching how OtelEnvResourceDetector parses the flat value.
    private static string FilterAttributesList(string list, HashSet<string> declaredNames)
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
            if (!declaredNames.Contains(index))
            {
                filtered.Add(trimmed);
            }
        }

        return JoinWithComma(filtered);
    }

#if NET
    [GeneratedRegex(AttributeNamePatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex GetAttributeNamePattern();
#else
    private static Regex GetAttributeNamePattern() => AttributeNamePatternInstance;
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
    private static string JoinWithComma(List<string> values) => string.Join(",", values);
#else
    private static string JoinWithComma(List<string> values) => string.Join(',', values);
#endif
}
