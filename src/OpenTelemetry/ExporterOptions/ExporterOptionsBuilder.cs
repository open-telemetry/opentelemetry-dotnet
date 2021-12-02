using System;
using System.Collections.Generic;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public abstract class ExporterOptionsBuilder<TOptions, TBuilder>
        where TOptions : class, new()
        where TBuilder : ExporterOptionsBuilder<TOptions, TBuilder>
    {
        private readonly List<Action<IServiceProvider, TOptions>> applyToActions = new();

        /// <summary>
        /// Gets the <typeparamref name="TBuilder"/> instance that will be used
        /// to chain configuration method calls to the builder.
        /// </summary>
        public abstract TBuilder BuilderInstance { get; }

        /// <summary>
        /// Gets the <typeparamref name="TOptions"/> instance used by the
        /// builder to store configuration until the build operation is
        /// executed.
        /// </summary>
        protected TOptions BuilderOptions { get; } = new();

        /// <summary>
        /// Configure the <typeparamref name="TOptions"/> created by the builder.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns><typeparamref name="TBuilder"/> for chaining.</returns>
        public TBuilder Configure(Action<TOptions> configure)
        {
            Guard.Null(configure);
            this.applyToActions.Add((sp, o) => configure(o));
            return this.BuilderInstance;
        }

        /// <summary>
        /// Configure the <typeparamref name="TOptions"/> created by the builder.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns><typeparamref name="TBuilder"/> for chaining.</returns>
        public TBuilder Configure(Action<IServiceProvider, TOptions> configure)
        {
            Guard.Null(configure);
            this.applyToActions.Add((sp, o) => configure(sp, o));
            return this.BuilderInstance;
        }

        /// <summary>
        /// Build an <typeparamref name="TOptions"/> instance from the builder
        /// configuration.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <returns><typeparamref name="TOptions"/> instance.</returns>
        public TOptions Build(IServiceProvider serviceProvider)
        {
            TOptions options = serviceProvider?.GetOptions<TOptions>() ?? new();

            this.ApplyTo(serviceProvider, options);

            int i = 0;
            while (i < this.applyToActions.Count)
            {
                this.applyToActions[i++](serviceProvider, options);
            }

            return options;
        }

        /// <summary>
        /// Fired when the final build operation is executing.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <param name="options"><typeparamref name="TOptions"/>.</param>
        protected virtual void ApplyTo(IServiceProvider serviceProvider, TOptions options)
        {
        }
    }
}
