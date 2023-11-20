using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abc.Zebus.Scan;

public class TypeSource
{
    public Func<Assembly, bool> AssemblyFilter { get; set; } = _ => true;
    public Func<Type, bool> TypeFilter { get; set; } = _ => true;

    public virtual IEnumerable<Type> GetTypes()
    {
        return AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Where(AssemblyFilter)
                        .SelectMany(a => a.GetTypes())
                        .Where(TypeFilter);
    }

    public static implicit operator TypeSource(Type type)
        => FromTypes(type);

    public static TypeSource FromAssembly(object objectFromAssembly)
    {
        var assembly = objectFromAssembly.GetType().Assembly;
        return new TypeSource
        {
            AssemblyFilter = x => x == assembly
        };
    }

    public static TypeSource FromType<T>()
        => FromTypes(typeof(T));

    public static TypeSource FromTypes<T1, T2>()
        => FromTypes(typeof(T1), typeof(T2));

    public static TypeSource FromTypes(params Type[] types)
        => new ListTypeSource(types);

    private class ListTypeSource : TypeSource
    {
        private readonly IEnumerable<Type> _types;

        public ListTypeSource(IEnumerable<Type> types)
        {
            _types = types;
        }

        public override IEnumerable<Type> GetTypes()
        {
            return _types;
        }
    }
}
