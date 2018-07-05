using System;

namespace Abc.Zebus.Transport.Zmq
{
    internal sealed class ZmqContext : IDisposable
    {
        public IntPtr Handle { get; private set; }

        public ZmqContext()
        {
            Handle = ZmqNative.ctx_new();

            if (Handle == IntPtr.Zero)
                ZmqUtil.ThrowLastError("Could not create ZMQ context");
        }

        ~ZmqContext()
        {
            Terminate(false);
        }

        public void Dispose()
        {
            Terminate(true);
            GC.SuppressFinalize(this);
        }

        private void Terminate(bool canThrow)
        {
            if (Handle == IntPtr.Zero)
                return;

            while (ZmqNative.ctx_term(Handle) == -1)
            {
                if (ZmqNative.errno() == ZmqErrorCode.EINTR)
                    continue;

                if (canThrow)
                    ZmqUtil.ThrowLastError("Could not terminate ZMQ context");

                break;
            }

            Handle = IntPtr.Zero;
        }
    }
}
