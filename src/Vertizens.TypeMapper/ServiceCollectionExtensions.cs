using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Vertizens.TypeMapper;

/// <summary>
/// Extensions to register services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all non-abstract classes that implement <see cref="ITypeMapper{TSource, TTarget}"/>.  Uses implementation types from calling assembly.
    /// Registers default implementation of <see cref="INameMatchTypeMapper{TSource, TTarget}"/> and <see cref="INameMatchTypeProjector{TSource, TTarget}"/>.
    /// </summary>
    public static IServiceCollection AddTypeMappers(this IServiceCollection services)
    {
        services.TryAddSingleton<ITypeMapper, TypeMapper>();
        services.TryAddSingleton(typeof(INameMatchTypeMapper<,>), typeof(NameMatchTypeMapper<,>));
        services.TryAddSingleton(typeof(ITypeMapper<,>), typeof(NameMatchTypeMapper<,>));
        services.TryAddSingleton(typeof(INameMatchTypeProjector<,>), typeof(NameMatchTypeProjector<,>));
        services.TryAddSingleton(typeof(ITypeProjector<,>), typeof(NameMatchTypeProjector<,>));

        RegisterGenericImplementations(services, Assembly.GetCallingAssembly(), typeof(ITypeMapper<,>));
        RegisterGenericImplementations(services, Assembly.GetCallingAssembly(), typeof(ITypeProjector<,>));

        return services;
    }

    private static void RegisterGenericImplementations(IServiceCollection services, Assembly assembly, Type genericType)
    {
        var allTypes = assembly.GetTypes();
        var implementationTypes = allTypes.Select(x =>
            new
            {
                Type = x,
                Interfaces = x.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericType).ToList()
            }).Where(x => x.Interfaces.Count > 0);

        foreach (var implementationType in implementationTypes)
        {
            foreach (var implementationInterface in implementationType.Interfaces)
            {
                services.TryAddSingleton(implementationInterface, implementationType.Type);
            }
        }
    }
}
