using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Vertizens.TypeMapper;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeMappers(this IServiceCollection services)
    {
        services.TryAddSingleton<ITypeMapper, TypeMapper>();
        services.TryAddSingleton(typeof(INameMatchTypeMapper<,>), typeof(NameMatchTypeMapper<,>));
        services.TryAddSingleton(typeof(ITypeMapper<,>), typeof(NameMatchTypeMapper<,>));
        services.TryAddSingleton(typeof(INameMatchTypeProjector<,>), typeof(NameMatchTypeProjector<,>));
        services.TryAddSingleton(typeof(ITypeProjector<,>), typeof(NameMatchTypeProjector<,>));

        RegisterTypeMappers(services, Assembly.GetCallingAssembly());

        return services;
    }

    private static void RegisterTypeMappers(IServiceCollection services, Assembly assembly)
    {
        var allTypes = assembly.GetTypes();
        var mapperTypes = allTypes.Select(x =>
            new
            {
                Type = x,
                MapperInterfaces = x.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypeMapper<,>)).ToList()
            }).Where(x => x.MapperInterfaces.Count > 0);

        foreach (var mapperType in mapperTypes)
        {
            foreach (var interfaceTypeMapper in mapperType.MapperInterfaces)
            {
                services.TryAddSingleton(interfaceTypeMapper, mapperType.Type);
            }
        }
    }
}
