using System;

namespace Abc.Zebus.Util
{
    internal static class ByteUtil
    {
        internal static void Copy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)
        {
            const int copyThreshold = 12;

            if (count > copyThreshold)
            {
                Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count);
            }
            else
            {
                var stop = srcOffset + count;
                for (var i = srcOffset; i < stop; i++)
                {
                    dst[dstOffset++] = src[i];
                }
            }
        }
    }
}
