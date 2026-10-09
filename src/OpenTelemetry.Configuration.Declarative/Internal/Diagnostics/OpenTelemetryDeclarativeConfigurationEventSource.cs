// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// EventSource for the OpenTelemetry declarative-configuration package.
/// </summary>
[EventSource(Name = "OpenTelemetry-Configuration-Declarative")]
internal sealed class OpenTelemetryDeclarativeConfigurationEventSource : EventSource
{
    public static readonly OpenTelemetryDeclarativeConfigurationEventSource Log = new();

    private const string StrictModeDocumentation = "https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Configuration.Declarative/README.md#strict-mode";

    [Event(1, Message = "Declarative config file_format warning: {0}", Level = EventLevel.Warning)]
    public void FileFormatWarning(string message) => this.WriteEvent(1, message);

    [Event(2, Message = "Declarative config: top-level section '{0}' is retained but not interpreted by this implementation.", Level = EventLevel.Warning)]
    public void UnknownConfigurationSection(string sectionName) => this.WriteEvent(2, sectionName);

    [Event(3, Message = "Declarative config: invalid resource attribute - {0}", Level = EventLevel.Warning)]
    public void InvalidResourceAttribute(string message) => this.WriteEvent(3, message);

    [Event(4, Message = "Declarative config: field '{0}' has non-boolean value '{1}'. Expected 'true' or 'false'. The setting will be ignored.", Level = EventLevel.Warning)]
    public void InvalidBooleanValue(string fieldName, string actualValue) => this.WriteEvent(4, fieldName, actualValue);

    [Event(5, Message = "Declarative config: YAML stream contains {0} document(s); only the first will be processed.", Level = EventLevel.Warning)]
    public void MultipleDocumentsDetected(int documentCount) => this.WriteEvent(5, documentCount);

    [Event(6, Message = "Declarative config: '{0}' section is malformed and will be ignored - {1}", Level = EventLevel.Warning)]
    public void MalformedSection(string sectionName, string message) => this.WriteEvent(6, sectionName, message);

    [Event(7, Message = "Declarative config: UseDeclarativeConfiguration has already been called on this IServiceCollection with '{0}'; the request to use '{1}' will be ignored. Only the first registered file path applies.", Level = EventLevel.Warning)]
    public void DeclarativeConfigurationAlreadyRegistered(string originalFilePath, string newFilePath) => this.WriteEvent(7, originalFilePath, newFilePath);

    [Event(8, Message = "Declarative config: registration started for file '{0}'.", Level = EventLevel.Verbose)]
    public void RegistrationStarted(string filePath) => this.WriteEvent(8, filePath);

    [Event(9, Message = "Declarative config: source registered for file '{0}'.", Level = EventLevel.Verbose)]
    public void SourceRegistered(string filePath) => this.WriteEvent(9, filePath);

    [Event(10, Message = "Declarative config: source for '{0}' is already registered in this builder; duplicate registration ignored.", Level = EventLevel.Verbose)]
    public void SourceAlreadyRegisteredInBuilder(string filePath) => this.WriteEvent(10, filePath);

    [Event(11, Message = "Declarative config: source for '{0}' already present in existing IConfiguration; chaining without duplicating.", Level = EventLevel.Verbose)]
    public void SourceAlreadyPresentInExistingConfiguration(string filePath) => this.WriteEvent(11, filePath);

    [NonEvent]
    public void FailedToLoadConfiguration(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedToLoadConfiguration(filePath, ex.ToInvariantString());
        }
    }

    [Event(12, Message = "Declarative config: failed to load configuration from '{0}': {1}", Level = EventLevel.Error)]
    public void FailedToLoadConfiguration(string filePath, string error) => this.WriteEvent(12, filePath, error);

    [Event(13, Message = "Declarative config: successfully loaded {1} key(s) from '{0}'.", Level = EventLevel.Verbose)]
    public void ConfigurationLoadSucceeded(string filePath, int keyCount) => this.WriteEvent(13, filePath, keyCount);

    [Event(14, Message = "Declarative config: 'disabled' is set in '{0}'; the SDK will produce no telemetry.", Level = EventLevel.Warning)]
    public void SdkDisabledDetected(string filePath) => this.WriteEvent(14, filePath);

    [Event(15, Message = "Declarative config: environment variable '{0}' is not set and has no default; substitution resolved to empty string.", Level = EventLevel.Verbose)]
    public void EnvironmentVariableNotSet(string variableName) => this.WriteEvent(15, variableName);

    [Event(16, Message = "Declarative config: environment variable '{0}' is set to an empty string and has no default; substitution resolved to empty string.", Level = EventLevel.Verbose)]
    public void EnvironmentVariableEmpty(string variableName) => this.WriteEvent(16, variableName);

    [Event(17, Message = "Declarative config: OTEL_CONFIG_FILE is not set; the registration is a no-op. Set OTEL_CONFIG_FILE to the path of your YAML configuration file to activate declarative configuration.", Level = EventLevel.Warning)]
    public void OtelConfigFileNotSet() => this.WriteEvent(17);

    [Event(18, Message = "Declarative config: resource.attributes contains a duplicate name '{0}'; only the first occurrence is used and this entry will be skipped.", Level = EventLevel.Warning)]
    public void DuplicateResourceAttributeName(string name) => this.WriteEvent(18, name);

    [Event(19, Message = "Declarative config: the existing IConfiguration descriptor could not be resolved to an IConfiguration instance when registering '{0}'; prior configuration will not be carried forward when declarative configuration is registered.", Level = EventLevel.Warning)]
    public void PriorConfigurationResolutionFailed(string filePath) => this.WriteEvent(19, filePath);

    [Event(20, Message = "Declarative config: no IConfiguration was registered at the time UseDeclarativeConfiguration was called for '{0}'. If host infrastructure registers IConfiguration after this call, it will take precedence and the declarative configuration source will be unreachable.", Level = EventLevel.Warning)]
    public void NoExistingConfigurationRegistered(string filePath) => this.WriteEvent(20, filePath);

    [Event(22, Message = "Declarative config: resource attribute name '{0}' does not follow the OTel attribute naming convention ([a-zA-Z_][-a-zA-Z0-9_.]*); it will be emitted as-is.", Level = EventLevel.Warning)]
    public void ResourceAttributeNameNotCompliant(string name) => this.WriteEvent(22, name);

    [Event(23, Message = "Declarative config: configuration file '{0}' is empty; no keys were produced and the SDK will use defaults.", Level = EventLevel.Informational)]
    public void EmptyConfigurationFile(string filePath) => this.WriteEvent(23, filePath);

    // The message deliberately avoids literal braces: EventSource manifest messages treat them as
    // format placeholders.
    [Event(24, Message = "Declarative config: '{0}' is not a complete environment variable substitution reference - there is no closing brace before the end of the value or the next '$$' escape - so it is left as literal text.", Level = EventLevel.Verbose)]
    public void UnresolvedSubstitutionExpression(string expression) => this.WriteEvent(24, expression);

    [Event(25, Message = "Declarative config: property '{0}' (line {1}, column {2}) is not defined by the OpenTelemetry configuration schema and is not permitted at this location. Check for a misspelling.", Level = EventLevel.Error)]
    public void UndefinedConfigurationProperty(string propertyPath, long line, long column) => this.WriteEvent(25, propertyPath, line, column);

    [Event(26, Message = "Declarative config: '{0}' has already been loaded. Declarative configuration is read once at start-up, so this reload had no effect and the configuration in use is unchanged.", Level = EventLevel.Warning)]
    public void ConfigurationReloadIgnored(string filePath) => this.WriteEvent(26, filePath);

    [Event(27, Message = "Declarative config: '{0}' was parsed on demand by a typed configuration consumer rather than by the configuration provider. The file is still read only once.", Level = EventLevel.Verbose)]
    public void DocumentParsedOnDemand(string filePath) => this.WriteEvent(27, filePath);

    [Event(28, Message = "Declarative config: no declarative configuration document is available.", Level = EventLevel.Verbose)]
    public void DocumentAccessorNotAvailable() => this.WriteEvent(28);

    [Event(
        29,
        Message = "Declarative config: source for '{0}' is already registered; the source for '{1}' will be ignored. Only the first declarative configuration file applies.",
        Level = EventLevel.Warning)]
    public void DifferentSourceAlreadyRegistered(string originalFilePath, string newFilePath) =>
        this.WriteEvent(29, originalFilePath, newFilePath);

    [Event(
        30,
        Message = "Declarative config: more than one declarative configuration file is reachable from the application's IConfiguration. Typed configuration consumers will use the document from '{0}', which is the file with the highest precedence, and ignore '{1}'. Individual flat configuration keys still follow standard IConfiguration ordering, so a flat value may come from a file other than '{0}'. Use one declarative configuration file per application.",
        Level = EventLevel.Warning)]
    public void MultipleConfigurationDocumentsReachable(string selectedFilePath, string ignoredFilePath) =>
        this.WriteEvent(30, selectedFilePath, ignoredFilePath);

    [Event(31, Message = "Declarative config: component provider '{0}' registered for component type '{1}' with name '{2}'.", Level = EventLevel.Verbose)]
    public void ComponentProviderRegistered(string providerType, string componentType, string name) => this.WriteEvent(31, providerType, componentType, name);

    [Event(32, Message = "Declarative config: created a component of type '{0}' with name '{1}'.", Level = EventLevel.Verbose)]
    public void ComponentCreated(string componentType, string name) => this.WriteEvent(32, componentType, name);

    [Event(33, Message = "Declarative config: no component provider is registered for component type '{0}' with name '{1}'. {2}", Level = EventLevel.Error)]
    public void ComponentProviderNotFound(string componentType, string name, string registeredNames) => this.WriteEvent(33, componentType, name, registeredNames);

    [Event(34, Message = "Declarative config: component type '{0}' with name '{1}' is claimed by both '{2}' and '{3}'. Each component type and name combination must be unique.", Level = EventLevel.Error)]
    public void DuplicateComponentProviderRejected(string componentType, string name, string existingProviderType, string duplicateProviderType) => this.WriteEvent(34, componentType, name, existingProviderType, duplicateProviderType);

    [Event(35, Message = "Declarative config: resource.attributes entry '{0}' has an integer value '{1}' that exceeds the 64-bit integer range and will be skipped.", Level = EventLevel.Warning)]
    public void UnrepresentableResourceAttributeInteger(string name, string value) => this.WriteEvent(35, name, value);

    [Event(
        36,
        Message = "Declarative config: strict mode is ignoring these OpenTelemetry settings because '{0}' is in use: {1}. See " + StrictModeDocumentation,
        Level = EventLevel.Warning)]
    public void StrictModeSettingsIgnored(string filePath, string keys) => this.WriteEvent(36, filePath, keys);

    [Event(
        37,
        Message = "Declarative config: configuration provider '{1}' is not masked by '{0}' and can override the document for {2}. See " + StrictModeDocumentation,
        Level = EventLevel.Warning)]
    public void LaterSourceOverridesStrictMode(string filePath, string providerType, string keys) => this.WriteEvent(37, filePath, providerType, keys);

    [Event(
        38,
        Message = "Declarative config: an environment variables configuration provider is not masked by '{0}' and can override the document for {1}. See " + StrictModeDocumentation,
        Level = EventLevel.Warning)]
    public void LaterEnvironmentVariablesOverrideStrictMode(string filePath, string keys) => this.WriteEvent(38, filePath, keys);

    [Event(
        39,
        Message = "Declarative config: using '{0}' as passed to UseDeclarativeConfiguration; OTEL_CONFIG_FILE '{1}' is ignored.",
        Level = EventLevel.Warning)]
    public void ExplicitFilePathOverridesConfigFile(string filePath, string configFileValue) => this.WriteEvent(39, filePath, configFileValue);

    [Event(40, Message = "Declarative config: strict mode diagnostics could not be evaluated: {0}", Level = EventLevel.Verbose)]
    public void StrictModeDiagnosticsUnavailable(string reason) => this.WriteEvent(40, reason);

    [Event(
        41,
        Message = "Declarative config: the source for '{0}' is not reachable from the application's IConfiguration, so strict mode cannot mask OTEL_* settings. See " + StrictModeDocumentation,
        Level = EventLevel.Warning)]
    public void StrictModeConfigurationSourceUnreachable(string filePath) => this.WriteEvent(41, filePath);

    [Event(
        42,
        Message = "Declarative config: top-level property '{0}' is not defined by the OpenTelemetry configuration schema. It is retained but the SDK does not interpret it.",
        Level = EventLevel.Informational)]
    public void UndefinedRootPropertyRetained(string propertyPath) => this.WriteEvent(42, propertyPath);

    [Event(
        43,
        Message = "Declarative config: property '{0}' is not defined by OpenTelemetry configuration schema {2}. It is retained but not applied: {3} (the document declares file_format '{1}').",
        Level = EventLevel.Warning)]
    public void UndefinedPropertyRetained(string propertyPath, string fileFormat, string schemaVersion, string reason) => this.WriteEvent(43, propertyPath, fileFormat, schemaVersion, reason);
}
