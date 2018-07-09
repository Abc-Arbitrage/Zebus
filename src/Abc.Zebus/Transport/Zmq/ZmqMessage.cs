using System.Runtime.InteropServices;

namespace Abc.Zebus.Transport.Zmq
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal unsafe ref struct ZmqMessage
    {
        public static void Init(ZmqMessage* message)
        {
            while (ZmqNative.msg_init(message) == -1)
            {
                if (ZmqUtil.WasInterrupted())
                    continue;

                ZmqUtil.ThrowLastError("Could not initialize ZMQ message");
            }
        }

        public static void Close(ZmqMessage* message)
        {
            while (ZmqNative.msg_close(message) == -1)
            {
                if (ZmqUtil.WasInterrupted())
                    continue;

                ZmqUtil.ThrowLastError("Could not close ZMQ message");
            }
        }
    }
}
