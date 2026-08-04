// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// Decides which OTEL_* environment variables reach the self-diagnostics preamble,
/// and what each one's value may show once it does.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsAllowed"/> governs inclusion: only variables in the allowlist (or matching
/// the dynamic auto-instrumentation per-integration patterns) are emitted. Unrecognised
/// OTEL_* variables - such as those consumed by the OpenTelemetry Collector or third-party
/// vendors - are silently omitted, keeping the preamble scoped to what the .NET SDK and
/// auto-instrumentation agent actually read.
/// </para>
/// <para>
/// <see cref="GetDisplayValue"/> governs disclosure, applying four rules in order.
/// Variables in <see cref="SensitiveVars"/> are redacted wholesale because their value
/// format is credential-carrying by definition. Any value that looks like inline key or
/// certificate material is redacted regardless of which variable holds it.
/// <c>OTEL_RESOURCE_ATTRIBUTES</c> is redacted per pair, so that <c>service.name</c> and
/// the other well-known deployment identifiers survive into support output while
/// unrecognised keys keep their values hidden. Endpoint variables are reduced to their
/// authority, dropping the credentials, path, query, and fragment that can carry tokens.
/// Everything else is shown verbatim.
/// </para>
/// </remarks>
internal static class SelfDiagnosticsEnvironmentVariablePolicy
{
    internal const string RedactedValue = "[REDACTED]";

    /// <summary>
    /// Variables whose values are always redacted because they can carry
    /// authentication credentials (e.g. <c>Authorization=Bearer ...</c>).
    /// </summary>
    internal static readonly HashSet<string> SensitiveVars = new(StringComparer.OrdinalIgnoreCase)
    {
        "OTEL_EXPORTER_OTLP_HEADERS",
        "OTEL_EXPORTER_OTLP_LOGS_HEADERS",
        "OTEL_EXPORTER_OTLP_METRICS_HEADERS",
        "OTEL_EXPORTER_OTLP_TRACES_HEADERS",
    };

    private const string ResourceAttributesVarName = "OTEL_RESOURCE_ATTRIBUTES";
    private const string PemArmourPrefix = "-----BEGIN";

    private static readonly char[] NewLineChars = ['\r', '\n'];
    private static readonly char[] ResourceAttributeKeyValueSeparator = ['='];

    /// <summary>
    /// Resource attribute keys whose values are safe to persist verbatim. These are the
    /// well-known identifiers a support bundle is read for; everything else keeps its key
    /// (a schema identifier, not a secret) and loses its value.
    /// </summary>
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

    /// <summary>
    /// Static allowlist of OTEL_* variables read by the .NET SDK
    /// (opentelemetry-dotnet) or the .NET auto-instrumentation agent
    /// (opentelemetry-dotnet-instrumentation).
    /// </summary>
    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
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

        "OTEL_EXPORTER_OTLP_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_CLIENT_KEY",
        "OTEL_EXPORTER_OTLP_COMPRESSION",
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_EXPORTER_OTLP_HEADERS",
        "OTEL_EXPORTER_OTLP_LOGS_COMPRESSION",
        "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_LOGS_HEADERS",
        "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL",
        "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT",
        "OTEL_EXPORTER_OTLP_METRICS_COMPRESSION",
        "OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION",
        "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_METRICS_HEADERS",
        "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL",
        "OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE",
        "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT",
        "OTEL_EXPORTER_OTLP_PROTOCOL",
        "OTEL_EXPORTER_OTLP_TIMEOUT",
        "OTEL_EXPORTER_OTLP_TRACES_COMPRESSION",
        "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
        "OTEL_EXPORTER_OTLP_TRACES_HEADERS",
        "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL",
        "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT",

        "OTEL_EXPORTER_PROMETHEUS_HOST",
        "OTEL_EXPORTER_PROMETHEUS_PORT",
        "OTEL_EXPORTER_ZIPKIN_ENDPOINT",

        "OTEL_CONFIG_FILE",
        "OTEL_EXPERIMENTAL_CONFIG_FILE",
        "OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED",
        "OTEL_LOG_LEVEL",
        "OTEL_LOGS_EXPORTER",
        "OTEL_METRIC_EXPORT_INTERVAL",
        "OTEL_METRIC_EXPORT_TIMEOUT",
        "OTEL_METRICS_EXEMPLAR_FILTER",
        "OTEL_METRICS_EXPORTER",
        "OTEL_PROPAGATORS",
        "OTEL_RESOURCE_ATTRIBUTES",
        "OTEL_SDK_DISABLED",
        "OTEL_SERVICE_NAME",
        "OTEL_TRACES_EXPORTER",
        "OTEL_TRACES_SAMPLER",
        "OTEL_TRACES_SAMPLER_ARG",

        "OTEL_DOTNET_EXPERIMENTAL_METRICS_EXEMPLAR_FILTER_HISTOGRAMS",
        "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH",
        "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES",
        "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY",

        "OTEL_DOTNET_AUTO_HOME",
        "OTEL_DOTNET_AUTO_LOG_DIRECTORY",

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
        "OTEL_DOTNET_AUTO_LOG_FILE_SIZE",
        "OTEL_DOTNET_AUTO_LOGGER",
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

    /// <summary>
    /// Variables whose values are URIs. Only the authority is persisted: the scheme, host,
    /// and port answer "where was this pointed", while the userinfo, path, query, and
    /// fragment that were stripped are the parts that carry tokens in signed-URL and
    /// API-key-in-query deployments.
    /// </summary>
    private static readonly HashSet<string> UriValueVars = new(StringComparer.OrdinalIgnoreCase)
    {
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
        "OTEL_EXPORTER_ZIPKIN_ENDPOINT",
    };

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="name"/> is a recognised
    /// OTEL_* variable read by the .NET SDK or auto-instrumentation agent.
    /// </summary>
    /// <param name="name">The environment variable name to test.</param>
    /// <returns><see langword="true"/> if the variable is in the allowlist or matches a known dynamic pattern.</returns>
    internal static bool IsAllowed(string name)
        => Allowlist.Contains(name) || MatchesDynamicPattern(name);

    /// <summary>
    /// Returns the portion of <paramref name="value"/> that is safe to persist in a
    /// diagnostics support file, redacting it wholesale, per pair, or down to a URI
    /// authority according to which variable holds it.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The configured value.</param>
    /// <returns>The value to include in diagnostics output.</returns>
    internal static string GetDisplayValue(string name, string value)
    {
        if (SensitiveVars.Contains(name))
        {
            return RedactedValue;
        }

        // The OTLP certificate variables are documented as paths to PEM files, and a path is
        // exactly what a support bundle needs to diagnose a failed handshake - so they are not
        // blanket-redacted. Guard against the misconfiguration where the material itself was
        // pasted into the variable: no path, endpoint, or scalar setting spans lines or opens
        // with PEM armour.
        if (ContainsInlineSecretMaterial(value))
        {
            return RedactedValue;
        }

        if (string.Equals(name, ResourceAttributesVarName, StringComparison.OrdinalIgnoreCase))
        {
            return RedactResourceAttributes(value);
        }

        if (!UriValueVars.Contains(name))
        {
            return value;
        }

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

    // Auto-instrumentation resolves per-integration vars at runtime using a format string.
    // Rather than enumerating every integration name in the static allowlist, we recognise
    // the patterns here.
    private static bool MatchesDynamicPattern(string name)
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
