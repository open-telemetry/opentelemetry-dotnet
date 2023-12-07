// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// Defines an interface for a Propagator of TextMap type,
/// which uses string key/value pairs to inject and extract
/// propagation data.
/// </summary>
public abstract class TextMapPropagator
{
    /// <summary>
    /// Gets the list of headers used by propagator. The use cases of this are:
    ///   * allow pre-allocation of fields, especially in systems like gRPC Metadata
    ///   * allow a single-pass over an iterator (ex OpenTracing has no getter in TextMap).
    /// </summary>
    public abstract ISet<string> Fields { get; }

    /// <summary>
    /// Injects the context into a carrier.
    /// </summary>
    /// <typeparam name="T">Type of an object to set context on. Typically HttpRequest or similar.</typeparam>
    /// <param name="context">The default context to transmit over the wire.</param>
    /// <param name="carrier">Object to set context on. Instance of this object will be passed to setter.</param>
    /// <param name="setter">Action that will set name and value pair on the object.</param>
    public abstract void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter);

    /// <summary>
    /// Extracts the context from a carrier.
    /// </summary>
    /// <typeparam name="T">Type of object to extract context from. Typically HttpRequest or similar.</typeparam>
    /// <param name="context">The default context to be used if Extract fails.</param>
    /// <param name="carrier">Object to extract context from. Instance of this object will be passed to the getter.</param>
    /// <param name="getter">Function that will return string value of a key with the specified name.</param>
    /// <returns>Context from it's text representation.</returns>
    public abstract PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter);
}