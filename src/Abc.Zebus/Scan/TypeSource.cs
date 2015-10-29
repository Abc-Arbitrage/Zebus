using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abc.Zebus.Scan
{
    public class TypeSource
    {
        public TypeSource()
        {
            AssemblyFilter = _ => true;
            TypeFilter = _ => true;
        }

        public Func<Assembly, bool> AssemblyFilter { get; set; }
        public Func<Type, bool> TypeFilter { get; set; }

        public virtual IEnumerable<Type> GetTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(AssemblyFilter).SelectMany(x => x.GetTypes()).Where(TypeFilter);
        }

        public static implicit operator TypeSource(Type type)
        {
            return FromTypes(type);
        }

        public static TypeSource FromAssembly(object objectFromAssembly)
        {
            var assembly = objectFromAssembly.GetType().Assembly;
            return new TypeSource
            {
                AssemblyFilter = x => x == assembly
            };
        }

        public static TypeSource FromType<T>()
        {
            return FromTypes(typeof(T));
        }

        public static TypeSource FromTypes<T1, T2>()
        {
            return FromTypes(typeof(T1), typeof(T2));
        }

        public static TypeSource FromTypes(params Type[] types)
        {
            return new ListTypeSource(types);
        }

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
}