﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Abc.Zebus.Transport.Zmq;

internal static unsafe class ZmqUtil
{
    [ContractAnnotation("=> halt"), DoesNotReturn]
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

    [ContractAnnotation("=> halt"), DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgOutOfRange()
        => throw new ArgumentOutOfRangeException();

    public static Version GetVersion()
    {
        int major = 0, minor = 0, patch = 0;
        ZmqNative.version(&major, &minor, &patch);
        return new Version(major, minor, patch);
    }
}
