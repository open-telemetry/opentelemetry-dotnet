// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Internal;

/// <summary>
/// Governs the value-disclosure policy for the <c>OTEL_*</c> environment variable section of the
/// self-diagnostics preamble.
/// </summary>
/// <remarks>
/// <para>
/// The default disposition for a value is <b>redacted</b>. A value is shown verbatim only when
/// its variable appears in <see cref="ValuesSafeToDisplay"/> (or matches one of the
/// auto-instrumentation per-integration boolean patterns).
/// </para>
/// <para>
/// Two variables are free-form but classifiable in part, so they are handled ahead of the safe
/// list: endpoint variables are reduced to their authority (dropping the userinfo, path, query,
/// and fragment that carry tokens in signed-URL deployments), and
/// <c>OTEL_RESOURCE_ATTRIBUTES</c> is redacted per key so the well-known deployment identifiers
/// survive while user-supplied keys keep their names and lose their values.
/// </para>
/// </remarks>
internal static class SelfDiagnosticsEnvironmentVariablePolicy
{
    internal const string RedactedValue = "[REDACTED]";

    private const string ResourceAttributesVarName = "OTEL_RESOURCE_ATTRIBUTES";
    private const string PemArmourPrefix = "-----BEGIN";

    private static readonly char[] NewLineChars = ['\r', '\n'];
    private static readonly char[] ResourceAttributeKeyValueSeparator = ['='];

    /// <summary>
    /// Variables whose values are safe to persist verbatim.
    /// </summary>
    private static readonly HashSet<string> ValuesSafeToDisplay = new(StringComparer.OrdinalIgnoreCase)
    {
        "OTEL_ATTRIBUTE_COUNT_LIMIT",
        "OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT",
        "OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT",
        "OTEL_LINK_ATTRIBUTE_COUNT_LIMIT",
        "OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT",
        "OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT",
        "OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT",
        "OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT",
        "OTEL_SPAN_EVENT_COUNT_LIMIT",
        "OTEL_SPAN_LINK_COUNT_LIMIT",

        "OTEL_BLRP_EXPORT_TIMEOUT",
        "OTEL_BLRP_MAX_EXPORT_BATCH_SIZE",
        "OTEL_BLRP_MAX_QUEUE_SIZE",
        "OTEL_BLRP_SCHEDULE_DELAY",
        "OTEL_BSP_EXPORT_TIMEOUT",
        "OTEL_BSP_MAX_EXPORT_BATCH_SIZE",
        "OTEL_BSP_MAX_QUEUE_SIZE",
        "OTEL_BSP_SCHEDULE_DELAY",

        "OTEL_EXPORTER_OTLP_COMPRESSION",
        "OTEL_EXPORTER_OTLP_LOGS_COMPRESSION",
        "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL",
        "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT",
        "OTEL_EXPORTER_OTLP_METRICS_COMPRESSION",
        "OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION",
        "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL",
        "OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE",
        "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT",
        "OTEL_EXPORTER_OTLP_PROTOCOL",
        "OTEL_EXPORTER_OTLP_TIMEOUT",
        "OTEL_EXPORTER_OTLP_TRACES_COMPRESSION",
        "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL",
        "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT",

        "OTEL_EXPORTER_OTLP_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_CLIENT_KEY",

        "OTEL_EXPORTER_PROMETHEUS_HOST",
        "OTEL_EXPORTER_PROMETHEUS_PORT",

        "OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED",
        "OTEL_LOG_LEVEL",
        "OTEL_LOGS_EXPORTER",
        "OTEL_METRIC_EXPORT_INTERVAL",
        "OTEL_METRIC_EXPORT_TIMEOUT",
        "OTEL_METRICS_EXEMPLAR_FILTER",
        "OTEL_METRICS_EXPORTER",
        "OTEL_PROPAGATORS",
        "OTEL_SDK_DISABLED",
        "OTEL_SERVICE_NAME",
        "OTEL_TRACES_EXPORTER",
        "OTEL_TRACES_SAMPLER",

        "OTEL_CONFIG_FILE",
        "OTEL_EXPERIMENTAL_CONFIG_FILE",

        "OTEL_DOTNET_EXPERIMENTAL_METRICS_EXEMPLAR_FILTER_HISTOGRAMS",
        "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH",
        "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES",
        "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY",

        "OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS",
        "OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY",
        "OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS",

        "OTEL_DOTNET_AUTO_HOME",
        "OTEL_DOTNET_AUTO_LOG_DIRECTORY",
        "OTEL_DOTNET_AUTO_LOG_FILE_SIZE",
        "OTEL_DOTNET_AUTO_LOGGER",

        "OTEL_DOTNET_AUTO_AZURE_APP_SERVICES",
        "OTEL_DOTNET_AUTO_CLR_DISABLE_OPTIMIZATIONS",
        "OTEL_DOTNET_AUTO_CLR_ENABLE_INLINING",
        "OTEL_DOTNET_AUTO_CLR_ENABLE_NGEN",
        "OTEL_DOTNET_AUTO_DUMP_ILREWRITE_ENABLED",
        "OTEL_DOTNET_AUTO_EXCLUDE_PROCESSES",
        "OTEL_DOTNET_AUTO_FAIL_FAST_ENABLED",
        "OTEL_DOTNET_AUTO_FLUSH_ON_UNHANDLEDEXCEPTION",
        "OTEL_DOTNET_AUTO_GRAPHQL_SET_DOCUMENT",
        "OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED",
        "OTEL_DOTNET_AUTO_LOGS_ENABLE_LOG4NET_BRIDGE",
        "OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE",
        "OTEL_DOTNET_AUTO_LOGS_ENABLED",
        "OTEL_DOTNET_AUTO_LOGS_INCLUDE_FORMATTED_MESSAGE",
        "OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED",
        "OTEL_DOTNET_AUTO_METRICS_ADDITIONAL_SOURCES",
        "OTEL_DOTNET_AUTO_METRICS_ENABLED",
        "OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED",
        "OTEL_DOTNET_AUTO_NETFX_REDIRECT_ENABLED",
        "OTEL_DOTNET_AUTO_OPENTRACING_ENABLED",
        "OTEL_DOTNET_AUTO_ORACLEMDA_SET_DBSTATEMENT_FOR_TEXT",
        "OTEL_DOTNET_AUTO_PLUGINS",
        "OTEL_DOTNET_AUTO_REDIRECT_ENABLED",
        "OTEL_DOTNET_AUTO_RESOURCE_DETECTOR_ENABLED",
        "OTEL_DOTNET_AUTO_RULE_ENGINE_ENABLED",
        "OTEL_DOTNET_AUTO_SETUP_SDK",
        "OTEL_DOTNET_AUTO_SQLCLIENT_NETFX_ILREWRITE_ENABLED",
        "OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_LEGACY_SOURCES",
        "OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES",
        "OTEL_DOTNET_AUTO_TRACES_ASPNET_INSTRUMENTATION_CAPTURE_REQUEST_HEADERS",
        "OTEL_DOTNET_AUTO_TRACES_ASPNET_INSTRUMENTATION_CAPTURE_RESPONSE_HEADERS",
        "OTEL_DOTNET_AUTO_TRACES_ASPNETCORE_INSTRUMENTATION_CAPTURE_REQUEST_HEADERS",
        "OTEL_DOTNET_AUTO_TRACES_ASPNETCORE_INSTRUMENTATION_CAPTURE_RESPONSE_HEADERS",
        "OTEL_DOTNET_AUTO_TRACES_ENABLED",
        "OTEL_DOTNET_AUTO_TRACES_GRPCNETCLIENT_INSTRUMENTATION_CAPTURE_REQUEST_METADATA",
        "OTEL_DOTNET_AUTO_TRACES_GRPCNETCLIENT_INSTRUMENTATION_CAPTURE_RESPONSE_METADATA",
        "OTEL_DOTNET_AUTO_TRACES_HTTP_INSTRUMENTATION_CAPTURE_REQUEST_HEADERS",
        "OTEL_DOTNET_AUTO_TRACES_HTTP_INSTRUMENTATION_CAPTURE_RESPONSE_HEADERS",
        "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED",
    };

    private static readonly HashSet<string> SafeResourceAttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "deployment.environment",
        "deployment.environment.name",
        "service.instance.id",
        "service.name",
        "service.namespace",
        "service.version",
    };

    private static readonly string[] SafeResourceAttributeKeyPrefixes =
    [
        "cloud.",
        "container.",
        "host.",
        "k8s.",
        "os.",
        "process.runtime.",
        "telemetry.distro.",
        "telemetry.sdk.",
    ];

    private static readonly HashSet<string> UriValueVars = new(StringComparer.OrdinalIgnoreCase)
    {
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
        "OTEL_EXPORTER_ZIPKIN_ENDPOINT",
    };

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="name"/>'s value may be shown verbatim
    /// under <see cref="EnvironmentVariableLogMode.KnownSafeValues"/>.
    /// </summary>
    /// <param name="name">The environment variable name to test.</param>
    /// <returns><see langword="true"/> when the value is safe to disclose.</returns>
    internal static bool ShouldDisplayValue(string name)
        => ValuesSafeToDisplay.Contains(name) || MatchesDynamicBooleanPattern(name);

    /// <summary>
    /// Returns the portion of <paramref name="value"/> that is safe to persist in a
    /// diagnostics support file under <see cref="EnvironmentVariableLogMode.KnownSafeValues"/>.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The configured value.</param>
    /// <returns>The value to include in diagnostics output.</returns>
    internal static string GetDisplayValue(string name, string value)
    {
        // Applied ahead of everything else, including the safe list: no path, endpoint, or scalar
        // setting legitimately spans lines or opens with PEM armour, so content that does is key
        // material pasted where a reference was expected.
        if (ContainsInlineSecretMaterial(value))
        {
            return RedactedValue;
        }

        if (string.Equals(name, ResourceAttributesVarName, StringComparison.OrdinalIgnoreCase))
        {
            return RedactResourceAttributes(value);
        }

        if (UriValueVars.Contains(name))
        {
            return GetUriAuthority(value);
        }

        return ShouldDisplayValue(name) ? value : RedactedValue;
    }

    private static string GetUriAuthority(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return RedactedValue;
        }

        try
        {
            var sanitized = new UriBuilder(uri)
            {
                UserName = string.Empty,
                Password = string.Empty,
                Path = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty,
            };

            return sanitized.Uri.GetLeftPart(UriPartial.Authority);
        }
        catch (UriFormatException)
        {
            return RedactedValue;
        }
    }

    private static bool ContainsInlineSecretMaterial(string value)
        => value.IndexOfAny(NewLineChars) >= 0
            || value.TrimStart().StartsWith(PemArmourPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Redacts <c>OTEL_RESOURCE_ATTRIBUTES</c> per key=value pair rather than wholesale.
    /// The variable is W3C Baggage-formatted and routinely carries <c>service.name</c>,
    /// <c>service.version</c>, and deployment metadata - the highest-value fields in the
    /// preamble - alongside arbitrary user-supplied keys that may not be safe to persist.
    /// </summary>
    /// <param name="value">The raw variable value.</param>
    /// <returns>The value with unrecognised keys' values replaced.</returns>
    private static string RedactResourceAttributes(string value)
    {
        var pairs = value.Split(',');
        var builder = StringBuilderCache.Acquire(value.Length);

        for (var i = 0; i < pairs.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendResourceAttribute(builder, pairs[i]);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    private static void AppendResourceAttribute(StringBuilder builder, string pair)
    {
        if (pair.Length == 0)
        {
            return;
        }

        var parts = pair.Split(ResourceAttributeKeyValueSeparator, 2);
        if (parts.Length != 2)
        {
            // No key to classify against, so nothing can be shown safely.
            builder.Append(RedactedValue);
            return;
        }

        builder.Append(parts[0]).Append('=');
        builder.Append(
            IsSafeResourceAttributeKey(parts[0].Trim())
                ? parts[1]
                : RedactedValue);
    }

    private static bool IsSafeResourceAttributeKey(string key)
    {
        if (SafeResourceAttributeKeys.Contains(key))
        {
            return true;
        }

        foreach (var prefix in SafeResourceAttributeKeyPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Auto-instrumentation resolves per-integration boolean vars at runtime using a format
    // string. Rather than enumerating every integration name in the safe list, we recognise the
    // patterns here. Both families are strictly boolean-valued.
    private static bool MatchesDynamicBooleanPattern(string name)
    {
        const string instrumentationSuffix = "_INSTRUMENTATION_ENABLED";

        if (name.EndsWith(instrumentationSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // OTEL_DOTNET_AUTO_{SIGNAL}_{INTEGRATION}_INSTRUMENTATION_ENABLED
            return name.StartsWith("OTEL_DOTNET_AUTO_LOGS_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("OTEL_DOTNET_AUTO_METRICS_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("OTEL_DOTNET_AUTO_TRACES_", StringComparison.OrdinalIgnoreCase);
        }

        // OTEL_DOTNET_AUTO_{DETECTOR}_RESOURCE_DETECTOR_ENABLED
        return name.EndsWith("_RESOURCE_DETECTOR_ENABLED", StringComparison.OrdinalIgnoreCase)
            && name.StartsWith("OTEL_DOTNET_AUTO_", StringComparison.OrdinalIgnoreCase);
    }
}
