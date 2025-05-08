// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

/// <summary>
/// Contains methods for building <see cref="Resource"/> instances.
/// </summary>
public class ResourceBuilder
{
    internal readonly List<IResourceDetector> ResourceDetectors = [];
    private static readonly Resource DefaultResource = PrepareDefaultResource();

    private ResourceBuilder()
    {
    }

    internal IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Creates a <see cref="ResourceBuilder"/> instance with default attributes
    /// added. See <a
    /// href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#semantic-attributes-with-sdk-provided-default-value">resource
    /// semantic conventions</a> for details.
    /// Additionally it adds resource attributes parsed from OTEL_RESOURCE_ATTRIBUTES, OTEL_SERVICE_NAME environment variables
    /// to a <see cref="ResourceBuilder"/> following the <a
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable">Resource
    /// SDK</a>.
    /// </summary>
    /// <returns>Created <see cref="ResourceBuilder"/>.</returns>
    public static ResourceBuilder CreateDefault()
        => new ResourceBuilder()
            .AddResource(DefaultResource)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

    /// <summary>
    /// Creates an empty <see cref="ResourceBuilder"/> instance.
    /// </summary>
    /// <returns>Created <see cref="ResourceBuilder"/>.</returns>
    public static ResourceBuilder CreateEmpty()
        => new();

    /// <summary>
    /// Clears the <see cref="Resource"/>s added to the builder.
    /// </summary>
    /// <returns><see cref="ResourceBuilder"/> for chaining.</returns>
    public ResourceBuilder Clear()
    {
        this.ResourceDetectors.Clear();

        return this;
    }

    /// <summary>
    /// Build a merged <see cref="Resource"/> from all the <see cref="Resource"/>s added to the builder.
    /// </summary>
    /// <returns><see cref="Resource"/>.</returns>
    public Resource Build()
    {
        Resource finalResource = Resource.Empty;

        foreach (IResourceDetector resourceDetector in this.ResourceDetectors)
        {
            if (resourceDetector is ResolvingResourceDetector resolvingResourceDetector)
            {
                resolvingResourceDetector.Resolve(this.ServiceProvider);
            }

            var resource = resourceDetector.Detect();
            if (resource != null)
            {
                finalResource = finalResource.Merge(resource);
            }
        }

        return finalResource;
    }

    /// <summary>
    /// Add a <see cref="IResourceDetector"/> to the builder.
    /// </summary>
    /// <param name="resourceDetector"><see cref="IResourceDetector"/>.</param>
    /// <returns>Supplied <see cref="ResourceBuilder"/> for call chaining.</returns>
    public ResourceBuilder AddDetector(IResourceDetector resourceDetector)
    {
        Guard.ThrowIfNull(resourceDetector);

        this.ResourceDetectors.Add(resourceDetector);

        return this;
    }

    /// <summary>
    /// Add a <see cref="IResourceDetector"/> to the builder which will be resolved using the application <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="resourceDetectorFactory">Resource detector factory.</param>
    /// <returns>Supplied <see cref="ResourceBuilder"/> for call chaining.</returns>
    public ResourceBuilder AddDetector(Func<IServiceProvider, IResourceDetector> resourceDetectorFactory)
    {
        Guard.ThrowIfNull(resourceDetectorFactory);

        return this.AddDetectorInternal(sp =>
        {
            if (sp == null)
            {
                throw new NotSupportedException("IResourceDetector factory pattern is not supported when calling ResourceBuilder.Build() directly.");
            }

            return resourceDetectorFactory(sp);
        });
    }

    internal ResourceBuilder AddDetectorInternal(Func<IServiceProvider?, IResourceDetector> resourceDetectorFactory)
    {
        Guard.ThrowIfNull(resourceDetectorFactory);

        this.ResourceDetectors.Add(new ResolvingResourceDetector(resourceDetectorFactory));

        return this;
    }

    internal ResourceBuilder AddResource(Resource resource)
    {
        Guard.ThrowIfNull(resource);

        this.ResourceDetectors.Add(new WrapperResourceDetector(resource));

        return this;
    }

    private static Resource PrepareDefaultResource()
    {
        var defaultServiceName = "unknown_service";

        try
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            if (!string.IsNullOrWhiteSpace(processName))
            {
                defaultServiceName = $"{defaultServiceName}:{processName}";
            }
        }
        catch
        {
            // GetCurrentProcess can throw PlatformNotSupportedException
        }

        return new Resource(new Dictionary<string, object>
        {
            [ResourceSemanticConventions.AttributeServiceName] = defaultServiceName,
        });
    }

    internal sealed class WrapperResourceDetector : IResourceDetector
    {
        private readonly Resource resource;

        public WrapperResourceDetector(Resource resource)
        {
            this.resource = resource;
        }

        public Resource Detect() => this.resource;
    }

    private sealed class ResolvingResourceDetector : IResourceDetector
    {
        private readonly Func<IServiceProvider?, IResourceDetector> resourceDetectorFactory;
        private IResourceDetector? resourceDetector;

        public ResolvingResourceDetector(Func<IServiceProvider?, IResourceDetector> resourceDetectorFactory)
        {
            this.resourceDetectorFactory = resourceDetectorFactory;
        }

        public void Resolve(IServiceProvider? serviceProvider)
        {
            this.resourceDetector = this.resourceDetectorFactory(serviceProvider)
                ?? throw new InvalidOperationException("ResourceDetector factory did not return a ResourceDetector instance.");
        }

        public Resource Detect()
        {
            var detector = this.resourceDetector;

            Debug.Assert(detector != null, "detector was null");

#pragma warning disable CA1508 // Avoid dead conditional code
            return detector?.Detect() ?? Resource.Empty;
#pragma warning restore CA1508 // Avoid dead conditional code
        }
    }
}
