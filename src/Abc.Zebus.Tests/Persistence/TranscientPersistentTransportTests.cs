using System.Threading;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Persistence
{
    public class TranscientPersistentTransportTests : PersistentTransportFixture
    {
        protected override bool IsPersistent => false;

        [Test]
        public void should_receive_messages_immediately_after_start()
        {
            Transport.Start();

            var message = new FakeCommand(123).ToTransportMessage();
            InnerTransport.RaiseMessageReceived(message);

            Wait.Until(() => MessagesForwardedToBus.Count == 1, 2.Seconds());
        }

        [Test]
        public void should_not_start_replay()
        {
            StartMessageReplayCommand.ShouldBeNull();
        }

        [Test]
        public void should_not_publish_a_MessageHandled_event_after_a_persistent_message_is_processed_by_a_non_persistent_host()
        {
            Transport.Start();

            var command = new FakeCommand(123).ToTransportMessage();
            InnerTransport.RaiseMessageReceived(command);
            Thread.Sleep(10);

            InnerTransport.ExpectNothing();
        }

        [Test]
        public void should_get_pending_send_count_from_inner_transport()
        {
            InnerTransport.PendingSendCount= 42;

            Transport.PendingSendCount.ShouldEqual(InnerTransport.PendingSendCount);
        }
    }
}