using System.Runtime.InteropServices;

namespace Abc.Zebus.Transport.Zmq
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe ref struct ZmqMessage
    {
        public static void Init(ZmqMessage* message)
        {
            while (ZmqNative.msg_init(message) == -1)
            {
                if (ZmqNative.errno() == ZmqErrorCode.EINTR)
                    continue;

                ZmqUtil.ThrowLastError("Could not initialize ZMQ message");
            }
        }

        public static void Close(ZmqMessage* message)
        {
            while (ZmqNative.msg_close(message) == -1)
            {
                if (ZmqNative.errno() == ZmqErrorCode.EINTR)
                    continue;

                ZmqUtil.ThrowLastError("Could not close ZMQ message");
            }
        }
    }
}
