using System;

namespace Abc.Zebus
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class NoScanAttribute : Attribute
    {
        
    }
}