using Abc.Zebus.Core;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Directory;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public class BusFactoryTests
    {
        [Test]
        public void should_dispatch_message_locally()
        {
            var handler = new Handler();

            var bus = new BusFactory()
                .WithHandlers(typeof(Handler))
                .ConfigureContainer(x => x.ForSingletonOf<Handler>().Use(handler))
                .CreateAndStartInMemoryBus();

            var task = bus.Send(new FakeCommand(123));
            task.Wait();
            task.IsCompleted.ShouldBeTrue();

            handler.FakeId.ShouldEqual(123);
        }

        [Test]
        public void should_send_message_to_peer()
        {
            var directory = new TestPeerDirectory();
            var transport = new TestTransport();
            var bus = new BusFactory().CreateAndStartInMemoryBus(directory, transport);

            var otherPeer = new Peer(new PeerId("Abc.Testing.Other.0"), "tcp://other-peer:1234");
            directory.Peers[otherPeer.Id] = otherPeer.ToPeerDescriptor(false, typeof(CommandWithRemoteHandler));

            bus.Send(new CommandWithRemoteHandler());

            var sentMessage = transport.Messages.ExpectedSingle();
            sentMessage.TransportMessage.MessageTypeId.GetMessageType().ShouldEqual(typeof(CommandWithRemoteHandler));

            var target = sentMessage.Targets.ExpectedSingle();
            target.Id.ShouldEqual(otherPeer.Id);
        }

        [ProtoContract]
        public class CommandWithRemoteHandler : ICommand
        {
        }

        public class Handler : IMessageHandler<FakeCommand>
        {
            public int FakeId;

            public void Handle(FakeCommand message)
            {
                FakeId = message.FakeId;
            }
        }
    }
}