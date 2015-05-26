using System;
using System.Collections.Generic;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class SyncMessageHandlerInvokerTests
    {
        [Test]
        public void should_apply_context()
        {
            var busMock = new Mock<IBus>();
            var handler = new MessageContextAwareCommandHandler();
            var container = new Container(x =>
            {
                x.For<IBus>().Use(busMock.Object);
                x.ForSingletonOf<MessageContextAwareCommandHandler>().Use(handler);
            });

            var invoker = new SyncMessageHandlerInvoker(container, typeof(MessageContextAwareCommandHandler), typeof(ScanCommand1));
            var messageContext = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);

            var invocationMock = new Mock<IMessageHandlerInvocation>();
            invocationMock.SetupGet(x => x.Context).Returns(messageContext);
            invocationMock.SetupGet(x => x.Message).Returns(new ScanCommand1());

            invoker.InvokeMessageHandlerAsync(invocationMock.Object).RunSynchronously();

            invocationMock.Verify(x => x.SetupForInvocation(handler));
        }

        [Test]
        public void should_inject_context_in_handler_constructor()
        {
            var container = new Container(x => x.For<IBus>().Use(new Mock<IBus>().Object));
            var invoker = new SyncMessageHandlerInvoker(container, typeof(CommandHandlerWithMessageContextInConstructor), typeof(CommandHandlerWithMessageContextInConstructorCommand));
            var messageContext = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);
            var command = new CommandHandlerWithMessageContextInConstructorCommand();
            var invocation = command.ToInvocation(messageContext);

            invoker.InvokeMessageHandler(invocation);

            command.Context.ShouldEqual(messageContext);
        }

        [Test]
        public void should_proxy_bus_with_message_context_aware_bus()
        {
            var busMock = new Mock<IBus>();
            var configurationMock = new Mock<IBusConfiguration>();
            var equalityComparer = StringComparer.OrdinalIgnoreCase;
            var container = new Container(x =>
            {
                x.ForSingletonOf<IBus>().Use(busMock.Object);
                x.ForSingletonOf<IBusConfiguration>().Use(configurationMock.Object);
                x.For<IEqualityComparer<string>>().Use(equalityComparer);
            });

            var invoker = new SyncMessageHandlerInvoker(container, typeof(CommandHandlerWithThreeConstructorArguments), typeof(ScanCommand1));
            var messageContext = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);

            var handler = (CommandHandlerWithThreeConstructorArguments)invoker.CreateHandler(messageContext);
            handler.Bus.ShouldNotEqual(busMock.Object);
            handler.Configuration.ShouldEqual(configurationMock.Object);
            handler.EqualityComparerFunc().ShouldEqual(equalityComparer);

            var bus = handler.Bus.ShouldBe<MessageContextAwareBus>();
            bus.InnerBus.ShouldEqual(busMock.Object);
        }

        [Test]
        public void should_instanciate_new_message_context_aware_bus_for_every_handler()
        {
            var busMock = new Mock<IBus>();
            var container = new Container(x => x.ForSingletonOf<IBus>().Use(busMock.Object));

            var invoker = new SyncMessageHandlerInvoker(container, typeof(CommandHandlerWithOneConstructorArgument), typeof(ScanCommand1));

            var messageContext1 = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);
            var handler1 = (CommandHandlerWithOneConstructorArgument)invoker.CreateHandler(messageContext1);

            var messageContext2 = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);
            var handler2 = (CommandHandlerWithOneConstructorArgument)invoker.CreateHandler(messageContext2);

            handler1.Bus.ShouldNotEqual(handler2.Bus);
            ((MessageContextAwareBus)handler1.Bus).InnerBus.ShouldEqual(((MessageContextAwareBus)handler2.Bus).InnerBus);
        }

        [Test]
        public void should_preserve_life_cycle()
        {
            var busMock = new Mock<IBus>();
            var container = new Container(x =>
            {
                x.ForSingletonOf<IBus>().Use(busMock.Object);
                x.ForSingletonOf<CommandHandlerWithOneConstructorArgument>().Use<CommandHandlerWithOneConstructorArgument>();
            });

            var invoker = new SyncMessageHandlerInvoker(container, typeof(CommandHandlerWithOneConstructorArgument), typeof(ScanCommand1));
            var messageContext = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);

            var handler1 = (CommandHandlerWithOneConstructorArgument)invoker.CreateHandler(messageContext);
            var handler2 = (CommandHandlerWithOneConstructorArgument)invoker.CreateHandler(messageContext);

            ReferenceEquals(handler1, handler2).ShouldBeTrue("references should be equal");
        }

        [Test, Ignore("Manual test")]
        public void MeasureHandlerCreationPerformances()
        {
            var busMock = new Mock<IBus>();
            var configurationMock = new Mock<IBusConfiguration>();
            var equalityComparer = StringComparer.OrdinalIgnoreCase;
            var container = new Container(x =>
            {
                x.ForSingletonOf<IBus>().Use(busMock.Object);
                x.ForSingletonOf<IBusConfiguration>().Use(configurationMock.Object);
                x.For<IEqualityComparer<string>>().Use(equalityComparer);
            });

            // 21/02/2014 CAO OneConstructor 612 024.8 iterations/sec

            MeasureHandlerCreationPerformances(container, typeof(CommandHandlerWithOneConstructorArgument));
            MeasureHandlerCreationPerformances(container, typeof(CommandHandlerWithTwoConstructorArguments));
            MeasureHandlerCreationPerformances(container, typeof(CommandHandlerWithThreeConstructorArguments));
        }

        private static void MeasureHandlerCreationPerformances(Container container, Type handlerType)
        {
            var invoker = new SyncMessageHandlerInvoker(container, handlerType, typeof(ScanCommand1));
            var messageContext = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);
            invoker.CreateHandler(messageContext);

            Measure.Execution(500000, () => invoker.CreateHandler(messageContext));
        }

        private class CommandHandlerWithMessageContextInConstructorCommand : ICommand
        {
            public MessageContext Context { get; set; }
        }

        private class CommandHandlerWithMessageContextInConstructor : IMessageHandler<CommandHandlerWithMessageContextInConstructorCommand>
        {
            public MessageContext Context { get; private set; }

            public CommandHandlerWithMessageContextInConstructor(MessageContext context)
            {
                Context = context;
            }

            public void Handle(CommandHandlerWithMessageContextInConstructorCommand message)
            {
                message.Context = Context;
            }
        }

        private class MessageContextAwareCommandHandler : IMessageHandler<ScanCommand1>, IMessageContextAware
        {
            public MessageContext Context { get; set; }
            public MessageContext StaticContext { get; set; }

            public void Handle(ScanCommand1 message)
            {
                StaticContext = MessageContext.Current;
            }
        }

        private class CommandHandlerWithZeroConstructorArgument : IMessageHandler<ScanCommand1>
        {
            public void Handle(ScanCommand1 message)
            {
            }
        }

        private class CommandHandlerWithOneConstructorArgument : IMessageHandler<ScanCommand1>
        {
            public readonly IBus Bus;

            public CommandHandlerWithOneConstructorArgument(IBus bus)
            {
                Bus = bus;
            }

            public void Handle(ScanCommand1 message)
            {
            }
        }

        private class CommandHandlerWithTwoConstructorArguments : IMessageHandler<ScanCommand1>
        {
            public readonly IBus Bus;
            public readonly IBusConfiguration Configuration;

            public CommandHandlerWithTwoConstructorArguments(IBus bus, IBusConfiguration configuration)
            {
                Bus = bus;
                Configuration = configuration;
            }

            public void Handle(ScanCommand1 message)
            {
            }
        }

        private class CommandHandlerWithThreeConstructorArguments : IMessageHandler<ScanCommand1>
        {
            public readonly IBus Bus;
            public readonly IBusConfiguration Configuration;
            public readonly Func<IEqualityComparer<string>> EqualityComparerFunc;

            public CommandHandlerWithThreeConstructorArguments(IBus bus, IBusConfiguration configuration, Func<IEqualityComparer<string>> equalityComparerFunc)
            {
                Bus = bus;
                Configuration = configuration;
                EqualityComparerFunc = equalityComparerFunc;
            }

            public void Handle(ScanCommand1 message)
            {
            }
        }
    }
}