// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Options;

#if NET6_0_OR_GREATER
internal sealed class SingletonDelegatingOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions> : DelegatingOptionsFactory<TOptions>
#else
internal sealed class SingletonDelegatingOptionsFactory<TOptions> : DelegatingOptionsFactory<TOptions>
#endif
    where TOptions : class
{
    private readonly Action<string, TOptions> optionsResetAction;
    private readonly Dictionary<string, TOptions> instances = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SingletonDelegatingOptionsFactory{TOptions}"/> class.
    /// </summary>
    /// <param name="optionsFactoryFunc">Factory delegate used to create <typeparamref name="TOptions"/> instances.</param>
    /// <param name="optionsResetAction">Delegate called to reset <typeparamref name="TOptions"/> instances.</param>
    /// <param name="configuration"><see cref="IConfiguration"/>.</param>
    /// <param name="setups">The configuration actions to run.</param>
    /// <param name="postConfigures">The initialization actions to run.</param>
    /// <param name="validations">The validations to run.</param>
    public SingletonDelegatingOptionsFactory(
        Func<IConfiguration, string, TOptions> optionsFactoryFunc,
        Action<string, TOptions> optionsResetAction,
        IConfiguration configuration,
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
        : base(optionsFactoryFunc, configuration, setups, postConfigures, validations)
    {
        Debug.Assert(optionsResetAction != null, "optionsResetAction was null");

        this.optionsResetAction = optionsResetAction!;
    }

    public override TOptions Create(string name)
    {
        lock (this.instances)
        {
            if (!this.instances.TryGetValue(name, out var instance))
            {
                instance = base.Create(name);
                this.instances.Add(name, instance);
                return instance;
            }

            this.optionsResetAction(name, instance);

            this.RunConfigurationsAndValidations(name, instance);

            return instance;
        }
    }
}
