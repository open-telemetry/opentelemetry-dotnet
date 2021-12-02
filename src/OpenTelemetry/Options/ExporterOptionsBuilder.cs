using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public abstract class ExporterOptionsBuilder<TOptions, TBuilder>
        where TOptions : class, new()
        where TBuilder : ExporterOptionsBuilder<TOptions, TBuilder>
    {
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
        public TOptions BuilderOptions { get; } = new();

        /// <summary>
        /// Configure the <typeparamref name="TOptions"/> being managed by the
        /// builder.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns><typeparamref name="TBuilder"/> for chaining.</returns>
        public TBuilder Configure(Action<TOptions> configure)
        {
            Guard.Null(configure);
            configure(this.BuilderOptions);
            return this.BuilderInstance;
        }

        internal TOptions Build(IServiceProvider serviceProvider)
        {
            TOptions options = serviceProvider?.GetOptions<TOptions>() ?? new();

            this.ApplyTo(options);

            return options;
        }

        /// <summary>
        /// Fired when the final build operation is executing.
        /// </summary>
        /// <param name="options"><typeparamref name="TOptions"/>.</param>
        protected virtual void ApplyTo(TOptions options)
        {
        }
    }
}
