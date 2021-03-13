using System;

namespace OpenTelemetry
{
    public static class ServiceProviderExtensions
    {
        private static readonly Type OptionsGeneircType = Type.GetType("Microsoft.Extensions.Options.IOptions`1, Microsoft.Extensions.Options");

        public static T GetOptions<T>(this IServiceProvider serviceProvider)
            where T : class, new()
        {
            if (OptionsGeneircType == null)
            {
                throw new InvalidOperationException("Microsoft.Extensions.Options.IOptions<> could not be found reflectively in the running process.");
            }

            Type optionsType = OptionsGeneircType.MakeGenericType(typeof(T));

            object options = serviceProvider.GetService(optionsType);

            return (T)optionsType.GetProperty("Value").GetMethod.Invoke(options, null);
        }
    }
}
