using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Abc.Zebus.Tests.Util
{
    [TestFixture]
    public class TypeUtilTests
    {
        [Test]
        public void should_resolve_type_from_current_assembly()
        {
            Assert.That(TypeUtil.Resolve(typeof(TypeUtilTests).FullName), Is.EqualTo(typeof(TypeUtilTests)));
        }

        [Test]
        public void should_resolve_type_from_other_assemblies()
        {
            Assert.That(TypeUtil.Resolve(typeof(PrefixOperator).FullName), Is.EqualTo(typeof(PrefixOperator)));
            Assert.That(TypeUtil.Resolve("System.String"), Is.Not.Null);
        }

        [Test]
        public void should_resolve_generic_types()
        {
            var localNamespace = typeof(TypeUtilTests).Namespace;
            var resolvedType = TypeUtil.Resolve(string.Format("{0}.GenericClass<System.String, {0}.OtherClass>", localNamespace));

            resolvedType.ShouldNotBeNull();
            resolvedType.ShouldEqual(typeof(GenericClass<string, OtherClass>));
        }

        [Test]
        public void should_resolve_nested_classes()
        {
            var localNamespace = typeof(TypeUtilTests).Namespace;
            var resolvedType = TypeUtil.Resolve(localNamespace + ".TypeUtilTests+NestedClass<System.String>");

            resolvedType.ShouldNotBeNull();
            resolvedType.ShouldEqual(typeof(NestedClass<string>));
        }

        [Test, MaxTime(2500)]
        public void should_resolve_type_fast()
        {
            for (int i = 0; i < 50000; ++i)
            {
                TypeUtil.Resolve("Invalid_Name_That_Requires_Iterating_All_Assemblies");
            }
        }

        public class NestedClass<T>
        {
        }
    }

    public class GenericClass<T, K>
    {
    }

    public class OtherClass
    {
    }
}
