using System;
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

            using (var context = ZContext.Create())
            {
                var sendEndpoint = $"tcp://localhost:{TcpUtil.GetRandomUnusedPort()}";
                var receiveEndpoint = sendEndpoint.Replace("localhost", "*");

                var receiver = new ZSocket(context, ZSocketType.PULL);
                receiver.ReceiveHighWatermark = 10;
                receiver.ReceiveTimeout = 200.Milliseconds();
                receiver.Bind(receiveEndpoint);

                var sender = new ZSocket(context, ZSocketType.PUSH);
                sender.SendHighWatermark = 10;
                sender.Connect(sendEndpoint);

                for (var i = 0; i < 10; ++i)
                {
                    var sendStatus = sender.Send(message, 0, message.Length);
                    Console.WriteLine(sendStatus);
                }
                for (var i = 0; i < 10; ++i)
                {
                    var bytes = receiver.ReceiveBytes(receiveBuffer, 0, receiveBuffer.Length);
                    Console.WriteLine(bytes);
                }

                receiver.Unbind(receiver.LastEndpoint);

                for (var i = 0; i < 10; ++i)
                {
                    var sendStatus = sender.Send(message, 0, message.Length);
                    Console.WriteLine(sendStatus);
                }

                sender.Disconnect(sender.LastEndpoint);
                sender.SendTimeout = 1000.Milliseconds();
                sender.Connect(sendEndpoint);

                var oneMoreSend = sender.Send(message, 0, message.Length);
                Console.WriteLine(oneMoreSend);

                receiver.ReceiveTimeout = 2000.Milliseconds();
                receiver.Bind(receiveEndpoint);
                
                var receivedMessageCount = 0;
                while (receiver.ReceiveBytes(receiveBuffer, 0, receiveBuffer.Length) != -1)
                {
                    ++receivedMessageCount;
                }

                Console.WriteLine("{0} received messages", receivedMessageCount);
            }
        }
    }
}