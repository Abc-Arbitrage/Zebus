using System;
using System.Runtime.InteropServices;

namespace Abc.Zebus.Transport.Zmq
{
    internal static partial class ZmqNative
    {
        private static readonly LibImpl _impl = GetLibImpl();

        private static unsafe LibImpl GetLibImpl()
        {
            try
            {
                var impl = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new WinCoreImpl()
                    : (LibImpl)new LinuxCoreImpl();

                int dummy;
                impl.version(&dummy, &dummy, &dummy);
                return impl;
            }
            catch (DllNotFoundException) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.Is64BitProcess
                    ? (LibImpl)new WinFramework64Impl()
                    : new WinFramework32Impl();
            }
        }
    }
}
