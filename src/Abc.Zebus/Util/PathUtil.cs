using System;
using System.Diagnostics;
using System.IO;

namespace Abc.Zebus.Util
{
    public static class PathUtil
    {
        public static string InBaseDirectory(string path)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        public static string InBaseDirectory(params string[] paths)
        {
            var allPaths = new string[paths.Length + 1];
            allPaths[0] = AppDomain.CurrentDomain.BaseDirectory;
            paths.CopyTo(allPaths, 1);
            return Path.Combine(allPaths);
        }

        public static string InCurrentNamespaceDirectory(params string[] paths)
        {
            var stack = new StackTrace();
            var callingFrame = stack.GetFrame(1);
            var callingType = callingFrame.GetMethod().DeclaringType;

            var rootNamespace = callingType.Assembly.GetName().Name;
            var classNamespace = callingType.Namespace;
            var extraNamespaces = classNamespace.Replace(rootNamespace, "").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            var allPaths = new string[extraNamespaces.Length + 1 + paths.Length];
            allPaths[0] = AppDomain.CurrentDomain.BaseDirectory;
            extraNamespaces.CopyTo(allPaths, 1);
            paths.CopyTo(allPaths, extraNamespaces.Length + 1);

            return Path.Combine(allPaths);
        }
    }
}