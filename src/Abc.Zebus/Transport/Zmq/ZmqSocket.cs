using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Abc.Zebus.Transport.Zmq
{
    internal unsafe class ZmqSocket : IDisposable
    {
        [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable", Justification = "Needed as a GC root")]
        private readonly ZmqContext _context;

        private IntPtr _handle;

        public ZmqSocket(ZmqContext context, ZmqSocketType type)
        {
            _context = context;

            _handle = ZmqNative.socket(_context.Handle, (int)type);

            if (_handle == IntPtr.Zero)
                ZmqUtil.ThrowLastError($"Could not create ZMQ {type} socket");
        }

        ~ZmqSocket()
        {
            Close(false);
        }

        public void Dispose()
        {
            Close(true);
            GC.SuppressFinalize(this);
        }

        private void Close(bool canThrow)
        {
            if (_handle == IntPtr.Zero)
                return;

            if (ZmqNative.close(_handle) == -1)
            {
                if (canThrow)
                    ZmqUtil.ThrowLastError("Could not close ZMQ socket");
            }

            _handle = IntPtr.Zero;
        }

        public void SetOption(ZmqSocketOption option, int value)
        {
            while (ZmqNative.setsockopt(_handle, (int)option, &value, (IntPtr)sizeof(int)) == -1)
            {
                if (ZmqUtil.WasInterrupted())
                    continue;

                ZmqUtil.ThrowLastError($"Unable to set ZMQ socket option {option} to {value}");
            }
        }

        public void SetOption(ZmqSocketOption option, byte[] value)
        {
            fixed (byte* valuePtr = value)
            {
                while (ZmqNative.setsockopt(_handle, (int)option, valuePtr, (IntPtr)(value?.Length ?? 0)) == -1)
                {
                    if (ZmqUtil.WasInterrupted())
                        continue;

                    ZmqUtil.ThrowLastError($"Unable to set ZMQ socket option {option}");
                }
            }
        }

        public string GetOptionString(ZmqSocketOption option)
        {
            const int bufSize = 256;
            var buf = stackalloc byte[bufSize];
            var size = (IntPtr)bufSize;

            while (ZmqNative.getsockopt(_handle, (int)option, buf, &size) == -1)
            {
                if (ZmqUtil.WasInterrupted())
                    continue;

                ZmqUtil.ThrowLastError($"Unable to get ZMQ socket option {option}");
            }

            if ((long)size <= 1)
                return string.Empty;

            return Marshal.PtrToStringAnsi((IntPtr)buf, (int)size - 1);
        }

        public void Bind(string endpoint)
        {
            if (ZmqNative.bind(_handle, endpoint) == -1)
                ZmqUtil.ThrowLastError($"Unable to bind ZMQ socket to {endpoint}");
        }

        public bool TryUnbind(string endpoint)
        {
            if (ZmqNative.unbind(_handle, endpoint) == -1)
                return false;

            return true;
        }

        public void Connect(string endpoint)
        {
            if (ZmqNative.connect(_handle, endpoint) == -1)
                ZmqUtil.ThrowLastError($"Unable to connect ZMQ socket to {endpoint}");
        }

        public bool TryDisconnect(string endpoint)
        {
            if (ZmqNative.disconnect(_handle, endpoint) == -1)
                return false;

            return true;
        }

        public bool TrySend(byte[] buffer, int offset, int count, out ZmqErrorCode error)
        {
            if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
                ZmqUtil.ThrowArgOutOfRange();

            if (count == 0)
            {
                error = ZmqErrorCode.None;
                return false;
            }

            fixed (byte* pBuf = &buffer[0])
            {
                while (true)
                {
                    var length = ZmqNative.send(_handle, pBuf + offset, (IntPtr)count, 0);
                    if (length >= 0)
                    {
                        error = ZmqErrorCode.None;
                        return true;
                    }

                    error = ZmqNative.errno();
                    if (error == ZmqErrorCode.EINTR)
                        continue;

                    return false;
                }
            }
        }

        public bool TryReadMessage(ref byte[] buffer, out int messageLength, out ZmqErrorCode error)
        {
            ZmqMessage message;
            ZmqMessage.Init(&message);

            try
            {
                while ((messageLength = ZmqNative.msg_recv(&message, _handle, 0)) == -1)
                {
                    error = ZmqNative.errno();
                    if (error == ZmqErrorCode.EINTR)
                        continue;

                    messageLength = 0;
                    return false;
                }

                if (messageLength <= 0)
                {
                    error = ZmqErrorCode.None;
                    return false;
                }

                if (buffer == null || buffer.Length < messageLength)
                    buffer = new byte[messageLength];

                fixed (byte* pBuf = &buffer[0])
                {
                    Buffer.MemoryCopy(ZmqNative.msg_data(&message), pBuf, buffer.Length, messageLength);
                }

                error = ZmqErrorCode.None;
                return true;
            }
            finally
            {
                ZmqMessage.Close(&message);
            }
        }
    }
}
