using System;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Extensions
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    internal class MyTestingAttributeAttribute : Attribute
    {
        public string Value { get; private set; }

        public MyTestingAttributeAttribute(string value)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    internal class MyTestingInheritedAttributeAttribute : MyTestingAttributeAttribute
    {
        public MyTestingInheritedAttributeAttribute(string value)
            : base(value)
        {
        }
    }

    internal interface ITestInterface { }

    [MyTestingInheritedAttribute("Value")]
    internal class TestClass : ITestInterface { }

    internal class TestSubClass : TestClass { }

    [MyTestingAttribute("Value1")]
    [MyTestingAttribute("Value2")]
    internal class TestGenericClass<T> { }

    [TestFixture]
    public class ExtendTypeTests
    {
        [Test]
        public void should_get_if_type_is_assignable()
        {
            typeof(TestClass).Is<ITestInterface>().ShouldBeTrue();
            typeof(ITestInterface).Is<TestClass>().ShouldBeFalse();
        }

        [Test]
        public void should_get_pretty_interface_name()
        {
            typeof(ITestInterface).GetPrettyName().ShouldEqual("ITestInterface");
            typeof(TestClass).GetPrettyName().ShouldEqual("TestClass");
            typeof(TestGenericClass<TestClass>).GetPrettyName().ShouldEqual("TestGenericClass<TestClass>");
        }

        [Test]
        public void should_return_attribute()
        {
            typeof(TestClass).GetAttribute<MyTestingInheritedAttributeAttribute>(false).ShouldEqualDeeply(new MyTestingInheritedAttributeAttribute("Value"));
        }

        [Test]
        public void should_return_inherited_attribute()
        {
            typeof(TestSubClass).GetAttribute<MyTestingAttributeAttribute>(true).ShouldEqualDeeply(new MyTestingInheritedAttributeAttribute("Value"));
        }

        [Test]
        public void should_return_null_if_the_attribute_is_not_defined()
        {
            typeof(ITestInterface).GetAttribute<Attribute>(false).ShouldBeNull();
        }

        [Test]
        public void should_throw_if_more_than_one_attribute_is_defined()
        {
            Assert.Throws<InvalidOperationException>(() => typeof(TestGenericClass<int>).GetAttribute<MyTestingAttributeAttribute>(false));
        }
    }
}