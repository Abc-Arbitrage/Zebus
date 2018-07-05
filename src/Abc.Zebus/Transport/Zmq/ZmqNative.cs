using System;

namespace Abc.Zebus.Transport.Zmq
{
    internal partial class ZmqNative
    {
        private static readonly LibImpl _impl = GetLibImpl();

        private static LibImpl GetLibImpl()
            => Environment.Is64BitProcess
                ? (LibImpl)new Win64Impl()
                : new Win32Impl();
    }
}
