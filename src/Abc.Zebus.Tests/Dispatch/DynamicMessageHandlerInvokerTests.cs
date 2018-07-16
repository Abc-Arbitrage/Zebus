using System;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class DynamicMessageHandlerInvokerTests
    {
        [Test]
        public void should_do_this()
        {
            var predicateBuilder = new Mock<IBindingKeyPredicateBuilder>();
            var bindingKey1 = new BindingKey("1", "toto", "*");
            var bindingKey2 = new BindingKey("1", "titi", "*");
            predicateBuilder.Setup(x => x.GetPredicate(It.IsAny<Type>(), bindingKey1)).Returns(_ => true);
            predicateBuilder.Setup(x => x.GetPredicate(It.IsAny<Type>(), bindingKey2)).Returns(_ => false);
            var handler = new DynamicMessageHandlerInvoker(message => { }, typeof(FakeRoutableCommand), new[]
            {
                bindingKey1,
                bindingKey2
            }, predicateBuilder.Object);
            
            var shouldBeHandled = handler.ShouldHandle(new FakeRoutableCommand(1, "lal"));

            shouldBeHandled.ShouldBeTrue();
            predicateBuilder.Verify(x => x.GetPredicate(It.IsAny<Type>(), It.IsAny<BindingKey>()), Times.Exactly(2));
        }

        [Test]
        public void should_handle_emtpy()
        {
            
        }
    }
}