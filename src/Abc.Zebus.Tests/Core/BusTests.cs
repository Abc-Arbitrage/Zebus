using System;
using System.Collections.Generic;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Serialization;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public abstract partial class BusTests
    {
        private const string _environment = "Test";

        private readonly Peer _self = new Peer(new PeerId("Abc.Testing.Self"), "tcp://abctest:123");
        private readonly Peer _peerUp = new Peer(new PeerId("Abc.Testing.Up"), "AnotherPeer");
        private readonly Peer _peerDown = new Peer(new PeerId("Abc.Testing.Down"), "tcp://abctest:999", false);

        private Bus _bus;
        private Mock<IBusConfiguration> _configuration;
        private TestTransport _transport;
        private Mock<IPeerDirectory> _directoryMock;
        private Mock<IMessageDispatcher> _messageDispatcherMock;
        private TestMessageSerializer _messageSerializer;
        private List<IMessageHandlerInvoker> _invokers;

        [SetUp]
        public virtual void Setup()
        {
            _configuration = new Mock<IBusConfiguration>();
            _transport = new TestTransport(_self.EndPoint);
            _directoryMock = new Mock<IPeerDirectory>();
            _messageDispatcherMock = new Mock<IMessageDispatcher>();
            _messageSerializer = new TestMessageSerializer();

            _bus = new Bus(_transport, _directoryMock.Object, _messageSerializer, _messageDispatcherMock.Object, new DefaultMessageSendingStrategy(), new DefaultStoppingStrategy(), Mock.Of<IBindingKeyPredicateBuilder>(), _configuration.Object);
            _bus.Configure(_self.Id, _environment);

            _invokers = new List<IMessageHandlerInvoker>();
            _messageDispatcherMock.Setup(x => x.GetMessageHanlerInvokers()).Returns(_invokers);

            _directoryMock.Setup(x => x.GetPeersHandlingMessage(It.IsAny<IMessage>())).Returns(new Peer[0]);
        }

        [TearDown]
        public virtual void Teardown()
        {
            if (System.IO.Directory.Exists(_bus.DeserializationFailureDumpDirectoryPath))
                System.IO.Directory.Delete(_bus.DeserializationFailureDumpDirectoryPath, true);
        }

        

        private void AddInvoker<TMessage>(bool shouldBeSubscribedOnStartup) where TMessage : class, IMessage
        {
            _invokers.Add(new TestMessageHandlerInvoker<TMessage>(shouldBeSubscribedOnStartup));
        }

        private void SetupPeersHandlingMessage<TMessage>(params Peer[] peers) where TMessage : IMessage
        {
            _directoryMock.Setup(x => x.GetPeersHandlingMessage(It.IsAny<TMessage>())).Returns(peers);
        }

        private void SetupDispatch<TMessage>(TMessage message, Action<IMessage> invokerCallback = null, Exception error = null) where TMessage : IMessage
        {
            _messageDispatcherMock.Setup(x => x.Dispatch(It.Is<MessageDispatch>(dispatch => dispatch.Message.DeepCompare(message))))
                                  .Callback<MessageDispatch>(dispatch =>
                                  {
                                      using (MessageContext.SetCurrent(dispatch.Context))
                                      {
                                          invokerCallback?.Invoke(dispatch.Message);

                                          dispatch.SetHandlerCount(1);

                                          var invokerMock = new Mock<IMessageHandlerInvoker>();
                                          invokerMock.SetupGet(x => x.MessageHandlerType).Returns(typeof(FakeMessageHandler));
                                          dispatch.SetHandled(invokerMock.Object, error);
                                      }
                                  });
        }

        private class FakeMessageHandler
        {
        }
    }
}