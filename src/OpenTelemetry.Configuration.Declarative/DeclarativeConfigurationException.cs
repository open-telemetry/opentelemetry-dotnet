// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !EXPOSE_EXPERIMENTAL_FEATURES
#pragma warning disable CA1064 // Exceptions should be public - intentionally internal in stable builds
#endif

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Configuration.Declarative;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// The exception thrown when a declarative configuration document cannot be
/// loaded or contains a semantic error (unsupported <c>file_format</c>, invalid
/// substitution reference, malformed structure, etc.).
/// </summary>
[Experimental(DiagnosticDefinitions.DeclarativeConfigurationExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
#if NETFRAMEWORK
[Serializable]
#endif
#if EXPOSE_EXPERIMENTAL_FEATURES
public
#else
internal
#endif
    sealed class DeclarativeConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class.
    /// </summary>
    public DeclarativeConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the error.</param>
    public DeclarativeConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public DeclarativeConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

#if NETFRAMEWORK
    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class
    /// with serialized data.
    /// </summary>
    /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data.</param>
    /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    private DeclarativeConfigurationException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
#endif
}
