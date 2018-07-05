using System;
using Abc.Zebus.Transport.Zmq;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    [Explicit]
    [Category("ManualOnly")]
    public class ZmqTests
    {
        [Test]
        public void OkNowIKnowThatMyMessagesAreLostAfterDisconnect()
        {
            var message = new byte[50];
            var receiveBuffer = new byte[100];

            Console.WriteLine("ZMQ v{0}", ZmqUtil.GetVersion().ToString(3));
            Console.WriteLine(Environment.Is64BitProcess ? "x64" : "x86");

            using (var context = new ZmqContext())
            using (var receiver = new ZmqSocket(context, ZmqSocketType.PULL))
            using (var sender = new ZmqSocket(context, ZmqSocketType.PUSH))
            {
                var sendEndpoint = $"tcp://localhost:{TcpUtil.GetRandomUnusedPort()}";
                var receiveEndpoint = sendEndpoint.Replace("localhost", "*");

                receiver.SetOption(ZmqSocketOption.RCVHWM, 10);
                receiver.SetOption(ZmqSocketOption.RCVTIMEO, 200);
                receiver.Bind(receiveEndpoint);

                sender.SetOption(ZmqSocketOption.SNDHWM, 10);
                sender.Connect(sendEndpoint);

                for (var i = 0; i < 10; ++i)
                {
                    var sendStatus = sender.TrySend(message, 0, message.Length, out var error);
                    Console.WriteLine($"SEND: {sendStatus} - {error.ToErrorMessage()}");
                }

                for (var i = 0; i < 10; ++i)
                {
                    var receiveStatus = receiver.TryReadMessage(ref receiveBuffer, out var bytes, out var error);
                    Console.WriteLine($"RECV: {receiveStatus} - {bytes} - {error.ToErrorMessage()}");
                }

                receiver.TryUnbind(receiver.GetOptionString(ZmqSocketOption.LAST_ENDPOINT));

                for (var i = 0; i < 10; ++i)
                {
                    var sendStatus = sender.TrySend(message, 0, message.Length, out var error);
                    Console.WriteLine($"SEND: {sendStatus} - {error.ToErrorMessage()}");
                }

                sender.TryDisconnect(sender.GetOptionString(ZmqSocketOption.LAST_ENDPOINT));
                sender.SetOption(ZmqSocketOption.SNDTIMEO, 1000);
                sender.Connect(sendEndpoint);

                {
                    var sendStatus = sender.TrySend(message, 0, message.Length, out var error);
                    Console.WriteLine($"SEND: {sendStatus} - {error.ToErrorMessage()}");
                }

                receiver.SetOption(ZmqSocketOption.RCVTIMEO, 2000);
                receiver.Bind(receiveEndpoint);

                var receivedMessageCount = 0;
                while (receiver.TryReadMessage(ref receiveBuffer, out _, out _))
                {
                    ++receivedMessageCount;
                }

                Console.WriteLine("{0} received messages", receivedMessageCount);
            }
        }
    }
}
