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
                    ? new WinImpl()
                    : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                        ? (LibImpl)new LinuxImpl()
                        : throw new PlatformNotSupportedException();

                int dummy;
                impl.version(&dummy, &dummy, &dummy);
                return impl;
            }
            catch (DllNotFoundException) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Used in the .NET Framework, and in .NET Core unit tests
                return Environment.Is64BitProcess
                    ? (LibImpl)new Win64Impl()
                    : new Win32Impl();
            }
            catch (DllNotFoundException) when (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Used in unit tests only
                return Environment.Is64BitProcess
                    ? (LibImpl)new Linux64Impl()
                    : new Linux32Impl();
            }
        }
    }
}
