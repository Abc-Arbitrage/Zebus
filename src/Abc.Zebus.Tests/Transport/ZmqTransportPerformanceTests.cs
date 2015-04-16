using System;
using System.Threading;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    [Ignore]
    [Category("ManualOnly")]
    public class ZmqTransportPerformanceTests
    {
        [Test]
        public void CreateIdleTransport()
        {
            var transport = CreateAndStartZmqTransport("Abc.Testing.Sender");

            Thread.Sleep(30000);

            transport.Stop();
        }

        [Test]
        public void MeasureThroughput()
        {
            const int sendMessageCount = 2000000;

            var senderTransport = CreateAndStartZmqTransport("Abc.Testing.Sender");

            var receivedMessageCount = 0;
            var receiverTransport = CreateAndStartZmqTransport("Abc.Testing.Receiver", _ => ++receivedMessageCount);
            var receivers = new[] { new Peer(receiverTransport.PeerId, receiverTransport.InboundEndPoint) };

            var transportMessage = new FakeCommand(42).ToTransportMessage();
            senderTransport.Send(transportMessage, receivers);

            var spinWait = new SpinWait();
            while (receivedMessageCount != 1)
                spinWait.SpinOnce();

            using (Measure.Throughput(sendMessageCount))
            {
                for (var i = 0; i < sendMessageCount; ++i)
                {
                    senderTransport.Send(transportMessage, receivers);
                }

                while (receivedMessageCount != sendMessageCount + 1)
                    spinWait.SpinOnce();
            }

            senderTransport.Stop();
            receiverTransport.Stop();
        }

        private ZmqTransport CreateAndStartZmqTransport(string peerId, Action<TransportMessage> onMessageReceived = null)
        {
            var configurationMock = new Mock<IZmqTransportConfiguration>();
            var transport = new ZmqTransport(configurationMock.Object, new ZmqSocketOptions());
            transport.Configure(new PeerId(peerId), "test");
            transport.SocketOptions.SendTimeout = 5.Seconds();

            if (onMessageReceived != null)
                transport.MessageReceived += onMessageReceived;

            transport.Start();
            return transport;
        }
    }
}