// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

/// <summary>
/// Contains extension methods for building <see cref="Resource"/>s.
/// </summary>
public static class ResourceBuilderExtensions
{
    private static readonly string InstanceId = Guid.NewGuid().ToString();

    private static Resource TelemetryResource { get; } = new Resource(new Dictionary<string, object>
    {
        [ResourceSemanticConventions.AttributeTelemetrySdkName] = "opentelemetry",
        [ResourceSemanticConventions.AttributeTelemetrySdkLanguage] = "dotnet",
        [ResourceSemanticConventions.AttributeTelemetrySdkVersion] = Sdk.InformationalVersion,
    });

    /// <summary>
    /// Adds service information to a <see cref="ResourceBuilder"/>
    /// following <a
    /// href="https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#service">semantic
    /// conventions</a>.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="serviceNamespace">Optional namespace of the service.</param>
    /// <param name="serviceVersion">Optional version of the service.</param>
    /// <param name="autoGenerateServiceInstanceId">Specify <see langword="true"/> to automatically generate a <see cref="Guid"/> for <paramref name="serviceInstanceId"/> if not supplied.</param>
    /// <param name="serviceInstanceId">Optional unique identifier of the service instance.</param>
    /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
    public static ResourceBuilder AddService(
        this ResourceBuilder resourceBuilder,
        string serviceName,
        string? serviceNamespace = null,
        string? serviceVersion = null,
        bool autoGenerateServiceInstanceId = true,
        string? serviceInstanceId = null)
    {
        Guard.ThrowIfNull(resourceBuilder);
        Guard.ThrowIfNullOrEmpty(serviceName);

        Dictionary<string, object> resourceAttributes = new Dictionary<string, object>();

        resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceName, serviceName);

        if (!string.IsNullOrEmpty(serviceNamespace))
        {
            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceNamespace, serviceNamespace!);
        }

        if (!string.IsNullOrEmpty(serviceVersion))
        {
            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceVersion, serviceVersion!);
        }

        if (serviceInstanceId == null && autoGenerateServiceInstanceId)
        {
            serviceInstanceId = InstanceId;
        }

        if (serviceInstanceId != null)
        {
            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceInstance, serviceInstanceId);
        }

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        return resourceBuilder.AddResource(new Resource(resourceAttributes));
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
    }

    /// <summary>
    /// Adds service information to a <see cref="ResourceBuilder"/>
    /// following <a
    /// href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk">semantic
    /// conventions</a>.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
    /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
    public static ResourceBuilder AddTelemetrySdk(this ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        return resourceBuilder.AddResource(TelemetryResource);
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
    }

    /// <summary>
    /// Adds attributes to a <see cref="ResourceBuilder"/>.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
    /// <param name="attributes">An <see cref="IEnumerable{T}"/> of attributes that describe the resource.</param>
    /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
    public static ResourceBuilder AddAttributes(this ResourceBuilder resourceBuilder, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        Guard.ThrowIfNull(resourceBuilder);
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        return resourceBuilder.AddResource(new Resource(attributes));
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
    }

    /// <summary>
    /// Adds resource attributes parsed from OTEL_RESOURCE_ATTRIBUTES, OTEL_SERVICE_NAME environment variables
    /// to a <see cref="ResourceBuilder"/> following the <a
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable">Resource
    /// SDK</a>.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
    /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
    public static ResourceBuilder AddEnvironmentVariableDetector(this ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);
        Lazy<IConfiguration> configuration = new Lazy<IConfiguration>(() => new ConfigurationBuilder().AddEnvironmentVariables().Build());

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        return resourceBuilder
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
            .AddDetectorInternal(sp => new OtelEnvResourceDetector(sp?.GetService<IConfiguration>() ?? configuration.Value))
            .AddDetectorInternal(sp => new OtelServiceNameEnvVarDetector(sp?.GetService<IConfiguration>() ?? configuration.Value));
    }
}
