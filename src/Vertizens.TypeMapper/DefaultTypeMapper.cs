using Microsoft.Extensions.DependencyInjection;

namespace Vertizens.TypeMapper;
internal class DefaultTypeMapper(IServiceProvider _serviceProvider) : ITypeMapper
{
    public TTarget Map<TSource, TTarget>(TSource source) where TTarget : class, new()
    {
        var target = new TTarget();
        Map(source, target);
        return target;
    }

    public void Map<TSource, TTarget>(TSource source, TTarget target)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        var typeMapper = (ITypeMapper<TSource, TTarget>)_serviceProvider.GetRequiredService(typeof(ITypeMapper<TSource, TTarget>));

        typeMapper.Map(source, target);
    }
}
