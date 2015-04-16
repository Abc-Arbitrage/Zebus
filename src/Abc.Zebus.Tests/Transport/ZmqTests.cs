using System;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using NUnit.Framework;
using ZeroMQ;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    [Ignore]
    [Category("ManualOnly")]
    public class ZmqTests
    {
        [Test]
        public void OkNowIKnowThatMyMessagesAreLostAfterDisconnect()
        {
            var message = new byte[50];
            var receiveBuffer = new byte[100];

            using (var context = ZmqContext.Create())
            {
                var sendEndpoint = string.Format("tcp://localhost:{0}", TcpUtil.GetRandomUnusedPort());
                var receiveEndpoint = sendEndpoint.Replace("localhost", "*");

                var receiver = context.CreateSocket(SocketType.PULL);
                receiver.ReceiveHighWatermark = 10;
                receiver.Bind(receiveEndpoint);

                var sender = context.CreateSocket(SocketType.PUSH);
                sender.SendHighWatermark = 10;
                sender.Connect(sendEndpoint);

                for (var i = 0; i < 10; ++i)
                {
                    var sendStatus = sender.Send(message);
                    Console.WriteLine(sendStatus);
                }
                for (var i = 0; i < 10; ++i)
                {
                    var bytes = receiver.Receive(receiveBuffer, 200.Milliseconds());
                    Console.WriteLine(bytes);
                }

                receiver.Unbind(receiveEndpoint);

                for (var i = 0; i < 10; ++i)
                {
                    var sendStatus = sender.Send(message);
                    Console.WriteLine(sendStatus);
                }

                sender.Disconnect(sendEndpoint);
                sender.Connect(sendEndpoint);

                var oneMoreSend = sender.SendWithTimeout(message, message.Length, 1000.Milliseconds());
                Console.WriteLine(oneMoreSend);

                receiver.Bind(receiveEndpoint);
                
                var receivedMessageCount = 0;
                while (receiver.Receive(receiveBuffer, 2000.Milliseconds()) != -1)
                {
                    ++receivedMessageCount;
                }

                Console.WriteLine("{0} received messages", receivedMessageCount);
            }
        }
    }
}