using System;

namespace Abc.Zebus.Transport.Zmq
{
    internal sealed class ZmqContext : IDisposable
    {
        internal IntPtr Handle { get; private set; }

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
                if (ZmqUtil.WasInterrupted())
                    continue;

                if (canThrow)
                    ZmqUtil.ThrowLastError("Could not terminate ZMQ context");

                break;
            }

            Handle = IntPtr.Zero;
        }

        public void SetOption(ZmqContextOption option, int value)
        {
            while (ZmqNative.ctx_set(Handle, (int)option, value) == -1)
            {
                if (ZmqUtil.WasInterrupted())
                    continue;

                ZmqUtil.ThrowLastError($"Unable to set ZMQ context option {option} to {value}");
            }
        }
    }
}
