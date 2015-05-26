using System;
using System.Collections.Generic;
using System.Reflection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Lotus;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Lotus
{
    [TestFixture]
    public class ReplayMessageHandlerTests
    {
        [Test]
        public void Should_replay_messages_on_failed_handlers()
        {
            var peer = new Peer(new PeerId("Test"), "test://test");
            var message = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), new byte[20], peer);

            var dispatcher = new FakeMessageDispatcher();
            var handler = new ReplayMessageHandler(dispatcher, new FakeDispatchFactory());
            handler.Handle(new ReplayMessageCommand(message, new[] { typeof(FakeHandler).FullName }));

            dispatcher.LastDispatchFilter.Invoke(typeof(FakeHandler)).ShouldBeTrue();
            dispatcher.LastDispatchFilter.Invoke(typeof(OtherFakeHandler)).ShouldBeFalse();
        }

        [Test]
        public void Should_replay_messages_on_all_handlers()
        {
            var peer = new Peer(new PeerId("Test"), "test://test");
            var message = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), new byte[20], peer);

            var dispatcher = new FakeMessageDispatcher();
            var handler = new ReplayMessageHandler(dispatcher, new FakeDispatchFactory());
            handler.Handle(new ReplayMessageCommand(message, new string[0]));

            dispatcher.LastDispatchFilter.Invoke(typeof(FakeHandler)).ShouldBeTrue();
            dispatcher.LastDispatchFilter.Invoke(typeof(OtherFakeHandler)).ShouldBeTrue();
        }


        private class FakeMessageDispatcher : IMessageDispatcher
        {
            public MessageDispatch LastDispatch;
            public Func<Type, bool> LastDispatchFilter;

            public void ConfigureAssemblyFilter(Func<Assembly, bool> assemblyFilter)
            {
                throw new NotSupportedException();
            }

            public void ConfigureHandlerFilter(Func<Type, bool> handlerFilter)
            {
                throw new NotSupportedException();
            }

            public void ConfigureMessageFilter(Func<Type, bool> messageFilter)
            {
                throw new NotSupportedException();
            }

            public void LoadMessageHandlerInvokers()
            {
                throw new NotSupportedException();
            }

            public IEnumerable<MessageTypeId> GetHandledMessageTypes()
            {
                throw new NotSupportedException();
            }

            public IEnumerable<IMessageHandlerInvoker> GetMessageHanlerInvokers()
            {
                throw new NotSupportedException();
            }

            public void Dispatch(MessageDispatch dispatch)
            {
                LastDispatch = dispatch;
                LastDispatchFilter = _ => true;
            }

            public void Dispatch(MessageDispatch dispatch, Func<Type, bool> handlerFilter)
            {
                LastDispatch = dispatch;
                LastDispatchFilter = handlerFilter;
            }

            public void Stop()
            {
                throw new NotSupportedException();
            }

            public void Start()
            {
                throw new NotSupportedException();
            }

            public void AddInvoker(IMessageHandlerInvoker eventHandlerInvoker)
            {
                throw new NotImplementedException();
            }

            public void RemoveInvoker(IMessageHandlerInvoker eventHandlerInvoker)
            {
                throw new NotImplementedException();
            }

            public int Purge()
            {
                throw new NotSupportedException();
            }

            public int MessageDispatchedInQueueCount { get; private set; }
            public void FlushMessageDispatchedInQueue()
            {
                throw new NotSupportedException();
            }
        }

        private class FakeDispatchFactory : IMessageDispatchFactory
        {
            public MessageDispatch CreateMessageDispatch(TransportMessage transportMessage)
            {
                return new MessageDispatch(MessageContext.CreateNew(transportMessage), null, null);
            }
        }

        private class FakeHandler : IMessageHandler<FakeCommand>
        {
            public void Handle(FakeCommand message)
            {
                throw new NotSupportedException();
            }
        }

        private class OtherFakeHandler : IMessageHandler<FakeCommand>
        {
            public void Handle(FakeCommand message)
            {
                throw new NotSupportedException();
            }
        }
    }
}
