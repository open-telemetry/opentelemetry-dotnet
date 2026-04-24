// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Options;

/// <summary>
/// An <see cref="OptionsFactory{TOptions}"/> that creates options instances via a
/// caller-supplied delegate, rather than <see cref="Activator.CreateInstance{T}"/>.
/// The delegate receives the <see cref="IConfiguration"/> registered in the DI
/// container, allowing options constructors to read environment variables and
/// configuration keys before any <see cref="IConfigureOptions{TOptions}"/> delegates
/// run. This establishes the SDK priority model:
/// <list type="number">
///   <item><description>Factory delegate — env var / <see cref="IConfiguration"/> defaults.</description></item>
///   <item><description><see cref="IConfigureOptions{TOptions}"/> (<c>Configure&lt;T&gt;</c>) — programmatic overrides.</description></item>
///   <item><description><see cref="IPostConfigureOptions{TOptions}"/> (<c>PostConfigure&lt;T&gt;</c>) — post-configuration.</description></item>
///   <item><description><see cref="IValidateOptions{TOptions}"/> — validation gate.</description></item>
/// </list>
/// </summary>
/// <typeparam name="TOptions">The options type to create.
/// The <c>DynamicallyAccessedMembersAttribute</c> is propagated from the base
/// class constraint so that trim/AOT analysis can verify callers preserve the public
/// parameterless constructor. The constructor itself is not used by this class (the
/// delegate creates the instance), but the attribute is required to satisfy the base
/// class type parameter contract.
/// </typeparam>
#if NET
internal sealed class DelegatingOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>
    : OptionsFactory<TOptions>
#else
internal sealed class DelegatingOptionsFactory<TOptions> : OptionsFactory<TOptions>
#endif
    where TOptions : class
{
    private readonly Func<IConfiguration, string, TOptions> optionsFactoryFunc;
    private readonly IConfiguration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatingOptionsFactory{TOptions}"/> class.
    /// </summary>
    /// <param name="optionsFactoryFunc">
    /// Delegate invoked by <see cref="CreateInstance"/> to construct each
    /// <typeparamref name="TOptions"/> instance. Receives the DI-registered
    /// <see cref="IConfiguration"/> and the options name.
    /// </param>
    /// <param name="configuration">
    /// Root <see cref="IConfiguration"/> passed to <paramref name="optionsFactoryFunc"/>
    /// on every call to <see cref="CreateInstance"/>.
    /// </param>
    /// <param name="setups">The <see cref="IConfigureOptions{TOptions}"/> delegates to apply after construction.</param>
    /// <param name="postConfigures">The <see cref="IPostConfigureOptions{TOptions}"/> delegates to apply after setup.</param>
    /// <param name="validations">The <see cref="IValidateOptions{TOptions}"/> validators to run last.</param>
    public DelegatingOptionsFactory(
        Func<IConfiguration, string, TOptions> optionsFactoryFunc,
        IConfiguration configuration,
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
        : base(setups, postConfigures, validations)
    {
        this.optionsFactoryFunc = optionsFactoryFunc;
        this.configuration = configuration;
    }

    /// <inheritdoc/>
    protected override TOptions CreateInstance(string name)
        => this.optionsFactoryFunc(this.configuration, name);
}
