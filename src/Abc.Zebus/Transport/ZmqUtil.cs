using System;
using System.IO;
using Abc.Zebus.Util;

namespace Abc.Zebus.Transport
{
    public static class ZmqUtil
    {
        internal static void ExtractLibZmq(string platform, string directory)
        {
            var directoryPath = PathUtil.InBaseDirectory(directory);
            if (!System.IO.Directory.Exists(directoryPath))
                System.IO.Directory.CreateDirectory(directoryPath);

            foreach (var libraryName in new[] { "libzmq", "libsodium" })
            {
                var libraryPath = PathUtil.InBaseDirectory(directory, $"{libraryName}.dll");
                if (File.Exists(libraryPath))
                    continue;

                var resourceName = $"{libraryName}-{platform}.dll";
                var transportType = typeof(ZmqTransport);
                using (var resourceStream = transportType.Assembly.GetManifestResourceStream(transportType, resourceName))
                {
                    if (resourceStream == null)
                        throw new Exception($"Unable to find {libraryName} in the embedded resources.");

                    using (var libraryFileStream = new FileStream(libraryPath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        resourceStream.CopyTo(libraryFileStream);
                    }
                }
            }
        }
    }
}