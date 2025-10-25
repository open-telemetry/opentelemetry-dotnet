// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Extended base processor base class.
/// </summary>
/// <typeparam name="T">The type of object to be processed.</typeparam>
[Experimental(DiagnosticDefinitions.ExtendedBaseProcessorExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#pragma warning disable CA1012 // Abstract types should not have public constructors
public abstract class ExtendedBaseProcessor<T> : BaseProcessor<T>
#pragma warning restore CA1012 // Abstract types should not have public constructors
{
    /// <summary>
    /// Called synchronously before a telemetry object ends.
    /// </summary>
    /// <param name="data">
    /// The started telemetry object.
    /// </param>
    /// <remarks>
    /// This function is called synchronously on the thread which ended
    /// the telemetry object. This function should be thread-safe, and
    /// should not block indefinitely or throw exceptions.
    /// </remarks>
    public virtual void OnEnding(T data)
    {
    }
}
#endif
