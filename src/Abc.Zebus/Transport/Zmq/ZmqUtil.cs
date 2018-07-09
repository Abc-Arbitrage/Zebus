using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus.Transport.Zmq
{
    internal static unsafe class ZmqUtil
    {
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Exception ThrowLastError(string message)
            => throw new IOException($"{message}: {GetLastErrorMessage()})");

        public static string ToErrorMessage(this ZmqErrorCode errorCode)
        {
            if (errorCode == ZmqErrorCode.None)
                return string.Empty;

            var errorStrBuf = ZmqNative.strerror((int)errorCode);
            if (errorStrBuf == null)
                return string.Empty;

            return $"{Marshal.PtrToStringAnsi((IntPtr)errorStrBuf)} (code {(int)errorCode})";
        }

        public static string GetLastErrorMessage()
            => ZmqNative.errno().ToErrorMessage();

        public static bool WasInterrupted()
            => ZmqNative.errno() == ZmqErrorCode.EINTR;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgOutOfRange()
            => throw new ArgumentOutOfRangeException();

        public static Version GetVersion()
        {
            int major, minor, patch;
            ZmqNative.version(&major, &minor, &patch);
            return new Version(major, minor, patch);
        }
    }
}
